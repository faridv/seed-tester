# Rockey4 Smart Password Tester - Quick Start Guide

## ⚠️ Important: Rockey4 Smart vs Rockey4ND

This application is for **Rockey4 Smart** devices only.
- Uses `Rockey4SClass.dll`
- Different API than Rockey4ND
- **Passwords change each time** (must be logged)

## Step 1: Verify Dongle Type

Confirm you have a **Rockey4 Smart** dongle:
- Check documentation
- Verify it uses `Rockey4SClass.dll`
- NOT Rockey4ND

## Step 2: Build the Application

```powershell
cd "C:\Users\farid\Desktop\Rockey Dongle\init-password\RockeyPasswordTester"
dotnet build -c Release
```

**DLL is already copied**: `Rockey4SClass.dll` is in the build directory.

## Step 3: Connect Your Dongle

1. Plug Rockey4 Smart dongle into USB
2. Wait for Windows to recognize it
3. Verify it's installed in Device Manager

## Step 4: Prepare Seed List

Create `seeds.txt` with one seed per line:

```
password123
mysecretkey
admin123
test1234
# Comments start with #
dongle1
dongle2
```

## Step 5: Run the Application

**Option 1**: Double-click `Run.bat`
**Option 2**: Double-click `RockeyPasswordTester.exe`
**Option 3**: Run from command line:
```powershell
.\RockeyPasswordTester.exe
```

## Step 6: Configure the Application

### Dongle Parameters

Keep defaults unless you know your dongle uses different values:
- P1: `6C6C`
- P2: `6B6B`
- P3: `8686`
- P4: `9090`

### Seed List File

1. Click "Browse..." (next to Seed List File)
2. Select your `seeds.txt` file

### Password Log File (CSV)

1. Click "Browse..." (next to Password Log File)
2. Choose where to save the log
3. Default: `password_log.csv`

**This CSV file will contain ALL seed → password mappings!**

## Step 7: Start Testing

1. Click "Start Testing"
2. Watch the status:
   - "Dongle opened. Testing X seeds..."
   - Progress updates every 10 seeds
   - Counters show tested seeds and successful tests

3. Results appear in real-time:
   ```
   ✓ Seed: password123          | Password: 6E07 646C B21F 77EA
   ✓ Seed: mysecretkey         | Password: 4A2F 1C8D 9B3E 5F7A
   ✓ Seed: test1234            | Password: 8C1D 3E7F 9A2B 4D6E
   ```

4. To stop testing, click "Cancel"

## Step 8: View Password Log

### Option 1: Open in Excel

1. Double-click `password_log.csv`
2. Excel opens with columns:
   - Seed
   - Password
   - Word1, Word2, Word3, Word4
   - Status
   - Timestamp

### Option 2: Open in Text Editor

```
Seed,Password,Word1,Word2,Word3,Word4,Status,Timestamp
"password123","6E07 646C B21F 77EA",6E07,646C,B21F,77EA,SUCCESS,2026-06-06 16:45:23
"mysecretkey","4A2F 1C8D 9B3E 5F7A",4A2F,1C8D,9B3E,5F7A,SUCCESS,2026-06-06 16:45:24
"test1234","8C1D 3E7F 9A2B 4D6E",8C1D,3E7F,9A2B,4D6E,SUCCESS,2026-06-06 16:45:25
```

## Step 9: Use Generated Passwords

### Since Passwords Change Each Time:

**Scenario 1: You Found a Good Seed**
1. Find the seed in the CSV log
2. Note the password
3. Use that password for your dongle
4. **Document which seed/password you used**

**Scenario 2: You Lost a Password**
1. Re-test the same seed
2. A new password will be generated
3. Update your CSV log
4. Update your documentation

**Scenario 3: Configure Multiple Dongles**
1. Test all seeds
2. Get passwords from CSV
3. Assign each seed/password to a dongle
4. **Keep the CSV secure and backed up**

## Step 10: Secure Your Data

### Critical Steps

1. **Backup the CSV file**:
   ```powershell
   Copy-Item password_log.csv password_log_backup.csv
   ```

2. **Document which passwords are used**:
   ```
   Dongle 1: seed="dongle1", password="6E07 646C B21F 77EA"
   Dongle 2: seed="dongle2", password="4A2F 1C8D 9B3E 5F7A"
   ```

3. **Never lose the CSV**:
   - Keep it secure
   - Make multiple backups
   - Encrypt if possible

## Example Complete Workflow

### Scenario: Set Up 5 New Dongles

#### Step 1: Create Seed List

```
sales_dept
support_dept
dev_dept
hr_dept
finance_dept
```

Save as `department_seeds.txt`

#### Step 2: Run Application

1. Select `department_seeds.txt`
2. Set log file to `department_passwords.csv`
3. Click "Start Testing"
4. Wait for completion

#### Step 3: View Results

Open `department_passwords.csv`:

```csv
Seed,Password,Word1,Word2,Word3,Word4,Status,Timestamp
"sales_dept","6E07 646C B21F 77EA",6E07,646C,B21F,77EA,SUCCESS,2026-06-06 16:45:23
"support_dept","4A2F 1C8D 9B3E 5F7A",4A2F,1C8D,9B3E,5F7A,SUCCESS,2026-06-06 16:45:24
"dev_dept","8C1D 3E7F 9A2B 4D6E",8C1D,3E7F,9A2B,4D6E,SUCCESS,2026-06-06 16:45:25
"hr_dept","2F4A 7B9C 1D3E 6F8A",2F4A,7B9C,1D3E,6F8A,SUCCESS,2026-06-06 16:45:26
"finance_dept","9E3B 5F1A 7C2D 4E8F",9E3B,5F1A,7C2D,4E8F,SUCCESS,2026-06-06 16:45:27
```

#### Step 4: Configure Dongles

1. Take first dongle
2. Use password: `6E07 646C B21F 77EA`
3. Label as "Sales Department"
4. Document in notes

Repeat for each dongle.

#### Step 5: Secure Documentation

```
README: Department Dongles
============================

Dongle 1 - Sales Department
- Seed: sales_dept
- Password: 6E07 646C B21F 77EA
- Location: Building A, Room 101
- Assigned: 2026-06-06

Dongle 2 - Support Department
- Seed: support_dept
- Password: 4A2F 1C8D 9B3E 5F7A
- Location: Building A, Room 102
- Assigned: 2026-06-06

(continue for all dongles...)

CSV File Location: department_passwords.csv
Backup Location: department_passwords_backup.csv
```

## Troubleshooting

### "Failed to open dongle"

**Checklist**:
- [ ] Dongle is Rockey4 Smart (not Rockey4ND)
- [ ] Dongle connected to USB
- [ ] Rockey4SClass.dll in same directory as .exe
- [ ] Running as Administrator
- [ ] Different USB port

### "Error code: 1"

- Dongle not found
- Check USB connection
- Verify dongle type

### "Error code: 255"

- Communication error
- Reconnect dongle
- Restart application

### CSV File Not Created

**Solutions**:
- Check file path is valid
- Ensure write permissions
- Check disk space
- Re-run test

### Application Won't Start

**Solutions**:
- Verify .NET 9.0 installed
- Check Rockey4SClass.dll is present
- Run as Administrator

## Tips for Success

### 1. Create Meaningful Seeds

```
# Good seeds
sales_team_2026
production_server_q2
backup_system_primary
license_manager_office

# Bad seeds
random123
asdfghjkl
password
```

### 2. Test in Batches

If you have many seeds:
- Test in batches of 100
- Save partial results
- Continue with next batch
- Keep backups of CSV files

### 3. Document Everything

Keep track of:
- Which seed → which password
- Which password → which dongle
- When each was generated
- Where each dongle is located

### 4. Regular Backups

```powershell
# Create backup
Copy-Item password_log.csv "password_log_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"

# Create scheduled backup
# (Use Windows Task Scheduler)
```

## What to Expect

### Testing 100 Seeds
- Time: ~5-10 seconds
- CSV size: ~10 KB
- Result: 100 seed → password mappings

### Testing 1,000 Seeds
- Time: ~1-2 minutes
- CSV size: ~100 KB
- Result: 1,000 seed → password mappings

### Testing 10,000 Seeds
- Time: ~10-20 minutes
- CSV size: ~1 MB
- Result: 10,000 seed → password mappings

## Security Best Practices

### 1. Secure the CSV File

```powershell
# Encrypt CSV file
$password = ConvertTo-SecureString "YourPassword" -AsPlainText -Force
Protect-CmsMessage -Path password_log.csv -To $password

# Decrypt CSV file
Unprotect-CmsMessage -Path password_log.csv.cms > password_log.csv
```

### 2. Limit Access

- Only run on trusted machines
- Don't share CSV files
- Delete old logs when no longer needed

### 3. Regular Security Audits

- Review who has access to CSV files
- Check for unauthorized access
- Rotate passwords regularly

## Next Steps

### After Testing

1. ✅ Verify CSV file is complete
2. ✅ Backup CSV file
3. ✅ Document seed → password mappings
4. ✅ Secure all documentation

### Using Passwords

1. ✅ Assign seeds/passwords to dongles
2. ✅ Label dongles clearly
3. ✅ Keep secure records
4. ✅ Never lose documentation

### Long-term Management

1. ✅ Regular backups
2. ✅ Security audits
3. ✅ Password rotation
4. ✅ Documentation updates

---

## Summary

You now have a **complete Rockey4 Smart password testing solution** that:

✅ Uses correct Rockey4 Smart API (`Rockey4SClass.dll`)
✅ Logs ALL passwords to CSV file
✅ Provides real-time progress
✅ Easy to use and configure

**The application is ready to use!**

**Remember**: Passwords change each time, so **always save the CSV log!**