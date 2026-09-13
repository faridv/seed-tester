using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RockeyPasswordTester
{
    // USB recovery, done the SAFE way.
    //
    // The old code power-cycled the dongle's *parent hub*. On this machine the wedged dongle's
    // only parent is the USB 3.0 ROOT HUB, so disabling it took down the whole controller -
    // keyboard, Bluetooth, mouse, everything - and often left them needing a driver reinstall.
    //
    // Every operation here is DEVICE-SCOPED and hard-limited to the Rockey VID/PID
    // (USB\VID_096E&PID_0006\...). We disable+enable only the dongle's own PnP node, which
    // forces Windows to re-enumerate that single device without disturbing anything else on the
    // bus. An instance id that is not a Rockey node is refused outright, so it is structurally
    // impossible for this code to cycle a hub or an unrelated device.
    internal static class UsbReset
    {
        public const string ROCKEY_HARDWARE_ID = @"USB\VID_096E&PID_0006";

        // Only one device reset may run at a time across the whole process, so several workers
        // wedging together don't fight over enabling/disabling the same nodes.
        private static readonly SemaphoreSlim _resetGate = new SemaphoreSlim(1, 1);

        public sealed class RockeyDevice
        {
            public string InstanceId = string.Empty;
            public string Status = string.Empty; // "OK", "Unknown", "Error", "Degraded", ...
            public bool IsHealthy => string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRockeyInstanceId(string id) =>
            !string.IsNullOrWhiteSpace(id) &&
            id.StartsWith(ROCKEY_HARDWARE_ID, StringComparison.OrdinalIgnoreCase);

        // Present Rockey dongle nodes with their PnP status. A wedged dongle keeps its node but
        // reports a non-"OK" status (we observed "Unknown"), which is how we tell it apart from a
        // healthy sibling that another worker is happily driving.
        public static List<RockeyDevice> GetPresentRockeyDevices()
        {
            var result = new List<RockeyDevice>();
            string script =
                "Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | " +
                "Where-Object { $_.InstanceId -like '" + EscapePsSingleQuoted(ROCKEY_HARDWARE_ID) + "*' } | " +
                "Sort-Object InstanceId | ForEach-Object { $_.Status + '|' + $_.InstanceId }";
            foreach (var line in RunPowerShellCaptureLines(script, 10000))
            {
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                string status = line.Substring(0, bar).Trim();
                string id = line.Substring(bar + 1).Trim();
                if (IsRockeyInstanceId(id)) result.Add(new RockeyDevice { Status = status, InstanceId = id });
            }
            return result;
        }

        public static List<string> GetPresentInstanceIds() =>
            GetPresentRockeyDevices()
                .Select(d => d.InstanceId)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Chooses which dongle node(s) to reset when one wedges. A dongle that has actually dropped
        // to a non-"OK" status is the ground truth for "which one is broken", and that beats the
        // best-effort (and possibly mis-paired) instance id - so we never bounce a healthy sibling.
        //   1. If any Rockey node is non-"OK" (visibly wedged): reset our own node when it is the
        //      wedged one, otherwise reset whatever nodes are wedged.
        //   2. If nothing looks wedged (a protocol-level stall while the node still reads "OK"):
        //      reset our own node if we know it, else every Rockey node.
        // Every branch stays limited to Rockey dongle nodes - never a hub or other device.
        public static List<string> ChooseResetTargets(string? preferredInstanceId)
        {
            var devices = GetPresentRockeyDevices();
            bool KnownPreferred(RockeyDevice d) =>
                !string.IsNullOrEmpty(preferredInstanceId) &&
                string.Equals(d.InstanceId, preferredInstanceId, StringComparison.OrdinalIgnoreCase);

            var wedged = devices.Where(d => !d.IsHealthy).ToList();
            if (wedged.Count > 0)
            {
                var mineWedged = wedged.FirstOrDefault(KnownPreferred);
                if (mineWedged != null) return new List<string> { mineWedged.InstanceId };
                return wedged.Select(d => d.InstanceId).ToList();
            }

            var mine = devices.FirstOrDefault(KnownPreferred);
            if (mine != null) return new List<string> { mine.InstanceId };
            return devices.Select(d => d.InstanceId).ToList();
        }

        // Disable + re-enable the given Rockey nodes. Non-Rockey ids are dropped defensively.
        // Runs elevated implicitly (the app requests admin in its manifest, so the child
        // powershell.exe inherits it and Disable/Enable-PnpDevice succeed).
        public static async Task<bool> ResetDevicesAsync(IReadOnlyCollection<string> instanceIds, CancellationToken ct, Action<string> log)
        {
            var safe = instanceIds.Where(IsRockeyInstanceId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (safe.Count == 0)
            {
                log("   No Rockey device node to reset (is the dongle plugged in?).");
                return false;
            }

            await _resetGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                string idsLiteral = string.Join(",", safe.Select(id => "'" + EscapePsSingleQuoted(id) + "'"));
                // Two device-scoped mechanisms, tried in order per node:
                //   1. `pnputil /restart-device` - the modern restart path. It succeeds on Rockey HID
                //      nodes where Disable-PnpDevice throws "Generic failure", and it re-enumerates the
                //      device (equivalent to a replug) without ever touching a hub.
                //   2. Disable+Enable-PnpDevice - fallback for older Windows / when pnputil is absent.
                // A node reports success if EITHER mechanism worked.
                string script = @"
$ErrorActionPreference = 'Continue'
$ids = @(" + idsLiteral + @")
$allOk = $true
foreach ($id in $ids) {
    $done = $false
    try {
        $out = & pnputil /restart-device ""$id"" 2>&1 | Out-String
        Write-Host (($out).Trim())
        if ($LASTEXITCODE -eq 0) { Write-Host ('Restarted ' + $id); $done = $true }
        else { Write-Host ('pnputil restart returned ' + $LASTEXITCODE + ' for ' + $id + '; trying disable/enable') }
    } catch { Write-Host ('pnputil restart error for ' + $id + ': ' + $_) }

    if (-not $done) {
        try {
            Disable-PnpDevice -InstanceId $id -Confirm:$false -ErrorAction Stop
            Write-Host ('Disabled ' + $id)
            Start-Sleep -Seconds 2
            Enable-PnpDevice -InstanceId $id -Confirm:$false -ErrorAction Stop
            Write-Host ('Enabled ' + $id)
            $done = $true
        } catch {
            Write-Host ('Disable/Enable failed for ' + $id + ': ' + $_)
        }
    }
    if (-not $done) { $allOk = $false }
}
Start-Sleep -Seconds 3
if ($allOk) { Write-Host 'Device reset complete'; exit 0 } else { Write-Host 'Device reset incomplete'; exit 3 }";
                foreach (var id in safe) log("   Resetting dongle node " + id);
                var (exit, outp, err) = await RunPowerShellAsync(script, 30000, ct).ConfigureAwait(false);

                string outTrim = outp.Trim();
                if (outTrim.Length > 0) log("   " + outTrim.Replace("\n", "\n   "));
                if (exit == 0)
                {
                    log("   Device reset done (you should hear the USB disconnect/reconnect chime).");
                    return true;
                }
                log($"   Device reset failed (exit {exit}).");
                if (!string.IsNullOrWhiteSpace(err)) log("   " + err.Trim());
                return false;
            }
            finally
            {
                _resetGate.Release();
            }
        }

        // ---- Opt-in hub power-cycle (last resort) ------------------------------------------
        // Some wedges hang the dongle's firmware so hard that a device-node restart brings it back as
        // an unrecognised device - only removing and re-applying BUS POWER to its port recovers it.
        // That means power-cycling the dongle's immediate parent hub. This is disruptive (it re-powers
        // every port on THAT hub), so it is opt-in, and we NEVER cycle a root hub (doing so takes down
        // the whole controller - keyboard, Bluetooth, etc., which was the original bug).

        public static bool IsRootHub(string? id) =>
            !string.IsNullOrEmpty(id) && id!.ToUpperInvariant().Contains("ROOT_HUB");

        // The dongle's immediate parent hub, or null if it can't be determined or is a root hub
        // (which we refuse to cycle). Query it while the dongle is healthy and remember it, because a
        // wedged dongle may drop off the bus and lose its parent link.
        public static string? GetCyclableParentHub(string dongleInstanceId)
        {
            if (string.IsNullOrWhiteSpace(dongleInstanceId)) return null;
            string script =
                "$p = (Get-PnpDeviceProperty -InstanceId '" + EscapePsSingleQuoted(dongleInstanceId) +
                "' -KeyName 'DEVPKEY_Device_Parent' -ErrorAction SilentlyContinue).Data; if ($p) { $p }";
            var lines = RunPowerShellCaptureLines(script, 8000);
            string? parent = lines.FirstOrDefault(l => l.Length > 0);
            if (string.IsNullOrEmpty(parent) || IsRootHub(parent)) return null;
            return parent;
        }

        // Friendly descriptions of everything currently on a hub, so the user can see what a cycle
        // will briefly disconnect.
        public static List<string> DescribeHubChildren(string hubInstanceId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(hubInstanceId)) return result;
            string script =
                "$c = (Get-PnpDeviceProperty -InstanceId '" + EscapePsSingleQuoted(hubInstanceId) +
                "' -KeyName 'DEVPKEY_Device_Children' -ErrorAction SilentlyContinue).Data; " +
                "foreach ($id in $c) { $d = Get-PnpDevice -InstanceId $id -ErrorAction SilentlyContinue; " +
                "if ($d) { $d.FriendlyName + ' [' + $d.Class + ']' } else { $id } }";
            foreach (var l in RunPowerShellCaptureLines(script, 8000))
                if (l.Trim().Length > 0) result.Add(l.Trim());
            return result;
        }

        // Power-cycles a single hub by disabling+enabling it. Refuses root hubs. Serialised with the
        // device-reset gate so it can't overlap another recovery.
        public static async Task<bool> CycleHubAsync(string hubInstanceId, CancellationToken ct, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(hubInstanceId)) { log("   No parent hub captured to cycle."); return false; }
            if (IsRootHub(hubInstanceId))
            {
                log("   Refusing to cycle a ROOT hub (that would disconnect every device on the controller).");
                return false;
            }

            await _resetGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                string hub = EscapePsSingleQuoted(hubInstanceId);
                string script = @"
$ErrorActionPreference = 'Stop'
try {
    $id = '" + hub + @"'
    $h = Get-PnpDevice -InstanceId $id -ErrorAction Stop
    Write-Host ('Power-cycling hub ' + $id + ' (' + $h.FriendlyName + ')')
    Disable-PnpDevice -InstanceId $id -Confirm:$false -ErrorAction Stop
    Write-Host 'Hub disabled (port power removed)'
    Start-Sleep -Seconds 3
    Enable-PnpDevice -InstanceId $id -Confirm:$false -ErrorAction Stop
    Write-Host 'Hub enabled (port re-powered)'
    Start-Sleep -Seconds 4
    Write-Host 'Hub power-cycle complete'
    exit 0
} catch { Write-Host ('Error: ' + $_); exit 3 }";
                log("   Power-cycling the dongle's hub " + hubInstanceId + " ...");
                var (exit, outp, err) = await RunPowerShellAsync(script, 30000, ct).ConfigureAwait(false);
                string o = outp.Trim();
                if (o.Length > 0) log("   " + o.Replace("\n", "\n   "));
                if (exit == 0) { log("   Hub power-cycled (you should hear the USB chime)."); return true; }
                log($"   Hub power-cycle failed (exit {exit}).");
                if (!string.IsNullOrWhiteSpace(err)) log("   " + err.Trim());
                return false;
            }
            finally { _resetGate.Release(); }
        }

        // ---- powershell plumbing ----------------------------------------------------------

        internal static List<string> RunPowerShellCaptureLines(string script, int timeoutMs)
        {
            var list = new List<string>();
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? p = null;
            try
            {
                p = Process.Start(psi);
                if (p == null) return list;

                // Drain both pipes async, enforce the timeout by killing a wedged process. A
                // synchronous ReadToEnd before WaitForExit would let a stalled powershell hang forever.
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                    return list;
                }
                string outp = string.Empty;
                try { outp = outTask.GetAwaiter().GetResult(); } catch { }
                try { _ = errTask.GetAwaiter().GetResult(); } catch { }
                foreach (var line in outp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) list.Add(trimmed);
                }
            }
            catch { }
            finally { try { p?.Dispose(); } catch { } }
            return list;
        }

        internal static async Task<(int exit, string outp, string err)> RunPowerShellAsync(string script, int timeoutMs, CancellationToken ct)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (-1, string.Empty, "could not start powershell.exe");
            using var reg = ct.Register(() => { try { process.Kill(entireProcessTree: true); } catch { } });

            Task<string> outTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errTask = process.StandardError.ReadToEndAsync();

            bool finished = await Task.Run(() => process.WaitForExit(timeoutMs)).ConfigureAwait(false);
            if (!finished)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return (-2, string.Empty, "reset timed out");
            }
            ct.ThrowIfCancellationRequested();

            string o = string.Empty, e = string.Empty;
            try { o = await outTask.ConfigureAwait(false); } catch { }
            try { e = await errTask.ConfigureAwait(false); } catch { }
            return (process.ExitCode, o, e);
        }

        private static string EscapePsSingleQuoted(string value) => value.Replace("'", "''");
    }
}
