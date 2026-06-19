# Troubleshooting Guide - DLL Loading Error

## Error Message

```
Error: Unable to load DLL 'Rockey4SClass.dll' or one of its dependencies: 
The specified module could not be found. (0x8007007E)
```

## Root Causes and Solutions

### Cause 1: Rockey4SClass.dll Missing or Wrong Location

**Symptom**: DLL loading error

**Solution**:
1. Ensure `Rockey4SClass.dll` is in the same folder as `RockeyPasswordTester.exe`
2. Check file exists:
   ```powershell
   dir "C:\Users\farid\Desktop\Rockey Dongle\init-password\RockeyPasswordTester\bin\Release\net9.0-windows\Rockey4SClass.dll"
   ```
3. If missing, copy from:
   - `C:\Program Files\Rockey\`
   - `C:\Program Files (x86)\Rockey\`
   - InitPassword.exe installation directory

### Cause 2: Missing Visual C++ Redistributable

**Symptom**: DLL loading error even though DLL is present

**Why**: `Rockey4SClass.dll` was built with Visual Studio and requires the C++ runtime

**Solution**:
1. Download Visual C++ 2015-2022 Redistributable (x86):
   ```
   https://aka.ms/vs/17/release/vc_redist.x86.exe
   ```

2. Install the redistributable

3. Restart the application

4. Verify installation:
   ```powershell
   reg query "HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86"
   ```

### Cause 3: Architecture Mismatch

**Symptom**: DLL loading error

**Why**: `Rockey4SClass.dll` is 32-bit (x86) but app is 64-bit (x64)

**Solution**:
The application is now built for **x86 (32-bit)**. Verify:
```powershell
cd "C:\Users\farid\Desktop\Rockey Dongle\init-password\RockeyPasswordTester"
type RockeyPasswordTester.csproj | findstr PlatformTarget
```

Should show:
```xml
<PlatformTarget>x86</PlatformTarget>
```

### Cause 4: Missing DLL Dependencies

**Symptom**: DLL loading error

**Why**: `Rockey4SClass.dll` depends on other DLLs

**Solution**:
1. Check with Dependency Walker:
   - Download: https://www.dependencywalker.com/
   - Open `Rockey4SClass.dll`
   - Look for missing DLLs (marked in red)

2. Common missing dependencies:
   - `msvcr120.dll` or `msvcr120d.dll`
   - `msvcp120.dll` or `msvcp120d.dll`
   - `vcruntime140.dll`
   - `msvcp140.dll`

3. Install Visual C++ Redistributable (see Cause 2)

## Step-by-Step Fix

### Step 1: Verify DLL Location

```powershell
cd "C:\Users\farid\Desktop\Rockey Dongle\init-password\RockeyPasswordTester\bin\Release\net9.0-windows"
dir *.dll *.exe
```

Expected output:
```
Rockey4SClass.dll
RockeyPasswordTester.dll
RockeyPasswordTester.exe
```

### Step 2: Install Visual C++ Redistributable

1. Download: https://aka.ms/vs/17/release/vc_redist.x86.exe

2. Run installer with Admin rights

3. Select "Repair" if already installed

4. Restart computer

### Step 3: Test Application

Run `Run.bat` or double-click `RockeyPasswordTester.exe`

If successful, you should see the application window.

### Step 4: If Still Failing

1. Use Dependency Walker:
   ```
   Download: https://www.dependencywalker.com/
   Open: Rockey4SClass.dll
   Check for red (missing) DLLs
   ```

2. Check Event Viewer:
   ```
   Event Viewer → Windows Logs → Application
   Look for error events related to Rockey4SClass.dll
   ```

3. Check System Requirements:
   - Windows 7 or later
   - x86 (32-bit) application
   - Visual C++ 2015-2022 Redistributable (x86)

## Quick Fix Script

Create and run this PowerShell script:

```powershell
# Check DLL exists
$dllPath = "C:\Users\farid\Desktop\Rockey Dongle\init-password\RockeyPasswordTester\bin\Release\net9.0-windows\Rockey4SClass.dll"
if (Test-Path $dllPath) {
    Write-Host "[OK] Rockey4SClass.dll found" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Rockey4SClass.dll NOT found" -ForegroundColor Red
    Write-Host "Please copy Rockey4SClass.dll to this directory"
}

# Check Visual C++ Redistributable
$vcRuntime = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86" -ErrorAction SilentlyContinue
if ($vcRuntime) {
    Write-Host "[OK] Visual C++ Redistributable (x86) installed" -ForegroundColor Green
} else {
    Write-Host "[WARNING] Visual C++ Redistributable (x86) may not be installed" -ForegroundColor Yellow
    Write-Host "Download from: https://aka.ms/vs/17/release/vc_redist.x86.exe"
}

# Check architecture
$appPath = "C:\Users\farid\Desktop\Rockey Dongle\init-password\RockeyPasswordTester\bin\Release\net9.0-windows\RockeyPasswordTester.exe"
if (Test-Path $appPath) {
    $bytes = [System.IO.File]::ReadAllBytes($appPath)
    $is32bit = $bytes[0x3C] -eq 0x0C -and $bytes[0x3D] -eq 0x00 -and $bytes[0x3E] -eq 0x02 -and $bytes[0x3F] -eq 0x00
    if ($is32bit) {
        Write-Host "[OK] Application is 32-bit (x86)" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] Application is NOT 32-bit" -ForegroundColor Red
    }
}
```

## Common Questions

### Q: Why do I need Visual C++ Redistributable?

**A**: The Rockey4SClass.dll was built using Microsoft Visual C++. It requires the C++ runtime library to function. This is a standard requirement for many applications.

### Q: Can I use the 64-bit version?

**A**: No. Rockey4SClass.dll is a 32-bit DLL. The application must also be 32-bit to load it.

### Q: Where can I find the original Rockey4SClass.dll?

**A**: 
- InitPassword.exe installation directory
- Rockey SDK download from manufacturer
- C:\Program Files\Rockey\ or C:\Program Files (x86)\Rockey\

### Q: I already have Visual C++ Redistributable installed. Why is it still failing?

**A**: 
1. Ensure you installed the **x86 (32-bit)** version, not x64
2. Try repairing the installation
3. Check if the correct version (2015-2022) is installed

## Summary

### Most Likely Solution

**Install Visual C++ 2015-2022 Redistributable (x86)**

Download: https://aka.ms/vs/17/release/vc_redist.x86.exe

This fixes 90% of DLL loading errors for applications built with Visual Studio.

### Next Steps

1. ✅ Download and install Visual C++ Redistributable (x86)
2. ✅ Restart computer
3. ✅ Run the application
4. ✅ If still failing, check with Dependency Walker

---

## Contact Support

If none of these solutions work:

1. Provide the exact error message
2. Screenshot of the error
3. Windows version: `winver`
4. Architecture: 32-bit or 64-bit Windows
5. Installed Visual C++ versions: "Visual C++ Redistributable" in Programs and Features

This will help identify the specific issue.