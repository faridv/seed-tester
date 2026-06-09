# Quick Reference - Rockey4 Smart Password Tester

## Basic Usage

### Step 1: Prepare Seeds
Create a text file with one seed per line:
```
seed1
seed2
seed3
```

### Step 2: Launch
Run `RockeyPasswordTester.exe`

### Step 3: Configure
- **SeedList**: Browse to your seed file
- **Log File**: Browse to save location
- **Passwords**: Default demo values (0xC44C, 0xC8F8, 0xCB51, 0x8C4E)

### Step 4: Test
Click "Test" to generate passwords

## Workflow

```
RY_FIND (1) → RY_OPEN (3) → RY_SEED (8) → RY_CLOSE (4)
     ↓              ↓             ↓              ↓
  Get Handle    Open Dongle   Generate     Close Dongle
```

## Code Example

```csharp
using Rockey4SmartClass;

var r4s = new Rockey4Smart();
ushort handle = 0;
uint lp1 = 0, lp2 = 0;
ushort p1 = 0xc44c, p2 = 0xc8f8, p3 = 0xCB51, p4 = 0x8C4E;
byte[] buffer = new byte[1024];

// Find
r4s.Rockey(1, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

// Open
r4s.Rockey(3, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

// Seed
lp2 = BitConverter.ToUInt32(seedBytes, 0);
r4s.Rockey(8, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

// Password is now in p1, p2, p3, p4

// Close
r4s.Rockey(4, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
```

## Variable Types

| Variable | Type | Purpose |
|----------|------|---------|
| handle | ushort | Dongle handle |
| lp1 | uint | Long parameter 1 |
| lp2 | uint | Long parameter 2 (seed) |
| p1-p4 | ushort | Passwords / Return codes |
| buffer | byte[1024] | Data buffer |

## Password Format

```
XXXX XXXX XXXX XXXX
(C44C) (C8F8) (CB51) (8C4E)
```

## Error Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 3 | Not found |
| 4 | Invalid password |
| 8 | Unknown command |

## Troubleshooting

### DLL Error
Install VC++ Redistributable: `https://aka.ms/vs/17/release/vc_redist.x86.exe`

### Dongle Not Found
- Check USB connection
- Run as Administrator
- Verify dongle type (Rockey4 Smart)

### Build Error
Ensure project targets x86 (32-bit)

## Files

- `RockeyPasswordTester.exe` - Main application
- `Rockey4SClass.dll` - SDK DLL (must be in same folder)
- `UPDATED_IMPLEMENTATION.md` - Full documentation

## Location

```
C:\Users\farid\Desktop\Rockey Dongle\init-password\RockeyPasswordTester\bin\Release\net9.0-windows\
```

---

**Key Changes**:
- ✅ Uses Rockey4SmartClass wrapper
- ✅ Proper workflow (FIND → OPEN → SEED → CLOSE)
- ✅ Correct variable types
- ✅ Comprehensive logging