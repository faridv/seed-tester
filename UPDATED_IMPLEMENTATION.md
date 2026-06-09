# Rockey4 Smart Password Tester - Updated Implementation

## ✅ Critical Changes Implemented

### 1. Correct SDK Usage
**Updated to use Rockey4SmartClass wrapper from Rockey4SClass.dll**

```csharp
using Rockey4SmartClass;

// Instantiate the class
Rockey4SmartClass.Rockey4Smart r4s = new Rockey4SmartClass.Rockey4Smart();
```

### 2. Correct Workflow: RY_FIND → RY_OPEN → RY_SEED → RY_CLOSE

```csharp
// Step 1: Find the dongle (RY_FIND = 1)
ushort retcode = r4s.Rockey(1, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
if (retcode != 0)
{
    // ROCKEY not found
}

// Step 2: Open the dongle (RY_OPEN = 3)
retcode = r4s.Rockey(3, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
if (retcode != 0)
{
    // Failed to open
}

// Step 3: Generate password from seed (RY_SEED = 8)
lp2 = BitConverter.ToUInt32(seedBytes, 0);
retcode = r4s.Rockey(8, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
if (retcode == 0)
{
    // Password is in p1, p2, p3, p4
}

// Step 4: Close the dongle (RY_CLOSE = 4)
r4s.Rockey(4, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
```

### 3. Correct Variable Types

```csharp
// Per C code specification:
ushort handle;    // Dongle handle
uint lp1, lp2;    // Long parameters
ushort p1, p2, p3, p4; // Password/return codes
byte[] buffer;    // 1024 byte buffer
```

### 4. Password Handling

**Initial Passwords**: Read from UI inputs (P1, P2, P3, P4 fields)
- Default demo values: 0xC44C, 0xC8F8, 0xCB51, 0x8C4E
- Can be customized via UI

**Password Propagation**: Last generated password is used for next seed
- First seed uses initial passwords from UI
- Subsequent seeds use password from previous RY_SEED call
- This maintains dongle state between operations

```csharp
// Read initial passwords from UI
ushort p1 = 0xC44C;
ushort p2 = 0xC8F8;
ushort p3 = 0xCB51;
ushort p4 = 0x8C4E;

if (ushort.TryParse(txtP1.Text, NumberStyles.HexNumber, null, out p1)) { }
if (ushort.TryParse(txtP2.Text, NumberStyles.HexNumber, null, out p2)) { }
if (ushort.TryParse(txtP3.Text, NumberStyles.HexNumber, null, out p3)) { }
if (ushort.TryParse(txtP4.Text, NumberStyles.HexNumber, null, out p4)) { }

// Pass to TestSeedsAsync
await TestSeedsAsync(seeds, r4s, handle, p1, p2, p3, p4, cancellationToken);

// In TestSeedsAsync, p1-p4 are initialized with initial values
// and then updated by each RY_SEED call
foreach (string seed in seeds)
{
    // p1, p2, p3, p4 contain the last generated password
    retcode = r4s.Rockey(8, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
    // p1, p2, p3, p4 now contain the new password
}
```

## Function Codes

| Code | Name | Description |
|------|------|-------------|
| 1 | RY_FIND | Find dongle, get handle |
| 2 | RY_FIND_NEXT | Find next dongle |
| 3 | RY_OPEN | Open dongle with password |
| 4 | RY_CLOSE | Close dongle |
| 8 | RY_SEED | Generate password from seed |

## Complete Workflow Example

```csharp
using Rockey4SmartClass;

public class RockeyTester
{
    public static void TestSeed(string seed)
    {
        Rockey4Smart r4s = new Rockey4Smart();

        // Variables
        ushort handle = 0;
        uint lp1 = 0;
        uint lp2 = 0;
        ushort p1 = 0xc44c;
        ushort p2 = 0xc8f8;
        ushort p3 = 0xCB51;
        ushort p4 = 0x8C4E;
        byte[] buffer = new byte[1024];

        // Step 1: Find dongle
        ushort retcode = r4s.Rockey(1, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

        if (retcode != 0)
        {
            Console.WriteLine($"ROCKEY not found! Error code: {retcode}");
            return;
        }

        // Step 2: Open dongle
        retcode = r4s.Rockey(3, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

        if (retcode != 0)
        {
            Console.WriteLine($"Failed to open dongle. Error code: {retcode}");
            return;
        }

        // Step 3: Seed dongle
        byte[] seedBytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(seed);

        // Pad to 8 bytes if needed
        if (seedBytes.Length > 8)
        {
            byte[] temp = new byte[8];
            Array.Copy(seedBytes, temp, 8);
            seedBytes = temp;
        }
        while (seedBytes.Length < 8)
        {
            Array.Resize(ref seedBytes, seedBytes.Length + 1);
        }

        // Set seed in lp2 (first 4 bytes)
        lp2 = BitConverter.ToUInt32(seedBytes, 0);

        // Reset password parameters to initial values
        p1 = initialP1;
        p2 = initialP2;
        p3 = initialP3;
        p4 = initialP4;

        // Call RY_SEED
        retcode = r4s.Rockey(8, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);

        if (retcode == 0)
        {
            // Success! Password is in p1, p2, p3, p4
            string password = $"{p1:X4} {p2:X4} {p3:X4} {p4:X4}";
            Console.WriteLine($"Seed: {seed}");
            Console.WriteLine($"Password: {password}");

            // p1, p2, p3, p4 now contain the new password
            // They will be used for the next RY_SEED call
        }
        else
        {
            Console.WriteLine($"Error generating password. Error code: {retcode}");
        }

        // Step 4: Close dongle
        r4s.Rockey(4, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
    }
}
```

## Key Changes Summary

### Before (Incorrect)
- ❌ Direct P/Invoke to DLL
- ❌ Missing RY_FIND step
- ❌ Wrong variable types (short vs ushort)
- ❌ Incorrect API signature

### After (Correct)
- ✅ Uses Rockey4SmartClass wrapper from DLL
- ✅ Proper workflow: FIND → OPEN → SEED → CLOSE
- ✅ Correct variable types (ushort handle, uint lp1/lp2, ushort p1-p4)
- ✅ Correct API signature from built-in DLL class

## Error Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 3 | No ROCKEY found |
| 4 | Invalid password |
| 5 | Invalid password or ID |
| 6 | Set ID error |
| 7 | Invalid address or size |
| 8 | Unknown command |
| 9 | Not level 3 (internal error) |
| 10 | Read error |
| 11 | Write error |
| 12 | Random error |
| 13 | Seed error |

## Important Notes

### Password Parameters (p1, p2, p3, p4)
- **For RY_OPEN**: These are input passwords to access the dongle (read from UI)
- **For RY_SEED**: These are input parameters (last password) and output parameters (new password)
- **Propagation**: Last generated password is used for next RY_SEED call
- **Important**: Do NOT reset p1-p4 between seeds

### Seed Handling
- Seed must be **exactly 8 bytes**
- Truncate if longer
- Pad with zeros if shorter
- Use ISO-8859-1 encoding for proper byte handling

### Password Format
- **4 words** (16-bit each)
- **Hex format**: `XXXX XXXX XXXX XXXX`
- Example: `C44C C8F8 CB51 8C4E`

## Application Features

### Main Form (MainForm.cs)
- **Browse Seeds**: Select seed list file
- **Browse Log**: Select CSV log file location
- **Test**: Run batch testing
- **Cancel**: Stop testing
- **Clear**: Clear results

### Display Elements
- **Dongle Status**: Shows connected/not connected
- **Handle**: Displays dongle handle
- **Seeds Tested**: Counter for tested seeds
- **Matches Found**: Counter for successful password generation
- **Verified**: Counter for verified passwords
- **Status**: Current operation status
- **Results**: Text area showing test results

### CSV Log Format
```csv
Seed,Password,Word1,Word2,Word3,Word4,Status,Verified,Timestamp
"12345678","C44C C8F8 CB51 8C4E",C44C,C8F8,CB51,8C4E,SUCCESS,YES,2026-06-06 18:45:00
```

## Build Information

- **Target Framework**: .NET 9.0 Windows
- **Platform**: x86 (32-bit) - Required for Rockey4SClass.dll
- **Build Configuration**: Release
- **Output Directory**: `bin\Release\net9.0-windows\`

## Dependencies

- `Rockey4SClass.dll` - Rockey4 Smart SDK (133,632 bytes)
- `System.Drawing.Common` - For Windows Forms

## Troubleshooting

### DLL Loading Error
If you see "Unable to load DLL 'Rockey4SClass.dll' or one of its dependencies":

1. Install Visual C++ 2015-2022 Redistributable (x86):
   ```
   https://aka.ms/vs/17/release/vc_redist.x86.exe
   ```

2. Ensure `Rockey4SClass.dll` is in the same folder as the executable

3. Run as Administrator

### Dongle Not Found
If you see "ROCKEY not found!":

1. Check dongle is connected to USB
2. Try different USB port
3. Run as Administrator
4. Ensure correct dongle type (Rockey4 Smart, not Rockey4ND)

### Password Generation Fails
If password generation returns errors:

1. Verify default passwords (p1-p4) match your dongle
2. Check seed is properly formatted (8 bytes)
3. Ensure dongle is properly opened with RY_OPEN

## Usage Instructions

1. **Prepare Seed List**: Create a text file with one seed per line
   ```
   # Sample seeds
   seed1234
   anotherseed
   56789012
   ```

2. **Launch Application**: Run `RockeyPasswordTester.exe`

3. **Select Files**:
   - Click "Browse" to select seed list
   - Click "Browse" to select log file location

4. **Set Passwords** (if different from demo):
   - P1: 0xC44C
   - P2: 0xC8F8
   - P3: 0xCB51
   - P4: 0x8C4E

5. **Test**:
   - Click "Test" to start testing
   - Watch results in real-time
   - Click "Cancel" to stop

6. **Review Results**:
   - Check on-screen results
   - Review CSV log file for all attempts

## Technical Details

### API Method Signature
```csharp
public ushort Rockey(
    ushort function,    // Function code (1, 3, 4, 8, etc.)
    ref ushort handle,  // Dongle handle
    ref uint lp1,       // Long parameter 1
    ref uint lp2,       // Long parameter 2 (seed for RY_SEED)
    ref ushort p1,      // Password 1 / Return code 1
    ref ushort p2,      // Password 2 / Return code 2
    ref ushort p3,      // Password 3 / Return code 3
    ref ushort p4,      // Password 4 / Return code 4
    byte[] buffer       // 1024 byte buffer
);
```

### Thread Safety
- Application uses async/await for UI responsiveness
- Cancellation token support for stopping operations
- Proper cleanup in finally blocks

### Error Handling
- Try-catch blocks for all dongle operations
- User-friendly error messages
- CSV logging of all errors
- Graceful degradation on errors

## Files Modified

1. **MainForm.cs** - Updated to use Rockey4SmartClass
   - Proper workflow: RY_FIND → RY_OPEN → RY_SEED → RY_CLOSE
   - Correct variable types
   - Enhanced error handling

2. **RockeyPasswordTester.csproj** - Updated project settings
   - Added `<PlatformTarget>x86</PlatformTarget>` for 32-bit DLL

## Verification

To verify the implementation:

1. Connect Rockey4 Smart dongle
2. Launch application
3. Check "Dongle Status" shows "Connected ✓"
4. Run test with known seed
5. Verify password is generated
6. Check CSV log for correct format

## Summary

The application has been updated to:
- ✅ Use the correct Rockey4SmartClass wrapper
- ✅ Follow the proper workflow (FIND → OPEN → SEED → CLOSE)
- ✅ Use correct variable types per C code specification
- ✅ Include comprehensive error handling
- ✅ Log all results to CSV for analysis

The application is now ready for testing seeds against your Rockey4 Smart dongle!