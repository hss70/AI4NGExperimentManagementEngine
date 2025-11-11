# AI4NG API Automated Testing

## 🚀 Quick Start

### Option 1: One-Click Newman (Recommended)
```powershell
# Install Newman
npm install -g newman

# Run all tests
.\scripts\run-api-tests.ps1
```

### Option 2: Postman GUI
1. Import `postman/AI4NG Automated Test Suite.postman_collection.json`
2. Click **"Run Collection"**
3. Click **"Run AI4NG Automated Test Suite"**

### Option 3: Windows Batch
```batch
# Double-click or run:
.\scripts\test-api.bat
```

## ✅ What Gets Tested

- **🔐 Authentication** - JWT token generation
- **📋 Task Management** - Create, retrieve, validate data integrity
- **🧪 Experiment Management** - Create with session types, validate data
- **📅 Session Management** - Create, retrieve, validate
- **🔄 Sync Endpoint** - Data consistency validation
- **🧹 Cleanup** - Delete test data

## 📊 Data Validation

Tests verify that **uploaded data = retrieved data**:
- Field-by-field comparison
- Nested object validation
- Cross-endpoint consistency
- Data type verification

## 🎯 Expected Output

```
✓ Authentication successful
✓ Task created successfully
✓ Task data matches uploaded data
✓ Experiment created successfully  
✓ Experiment data matches uploaded data
✓ Session created successfully
✓ Sync data validation passed
✓ Cleanup completed
```

## 🔧 Configuration

Current settings (pre-configured):
- **API URL**: `https://3mybicfkv2.execute-api.eu-west-2.amazonaws.com/dev`
- **Client ID**: `517s6c84jo5i3lqste5idb0o4c`
- **Username**: `hss702`
- **Password**: `Hardeep123!`

## 📁 Files

- `postman/AI4NG Automated Test Suite.postman_collection.json` - Main test collection
- `scripts/run-api-tests.ps1` - PowerShell automation script
- `scripts/test-api.bat` - Windows batch script

**Just run and verify everything works! 🎉**