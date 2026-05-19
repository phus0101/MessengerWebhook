# Phase 07: csproj Cleanup

**Priority**: P3  
**Effort**: 0.5 ngày  
**Status**: Complete  
**Depends on**: None (hoàn toàn độc lập)

---

## Vấn đề

`MessengerWebhook.csproj` có ~80 dòng `_ContentIncludedByDefault Remove` với đường dẫn đệ quy sâu do build artifacts bị include nhầm:

```xml
<_ContentIncludedByDefault Remove="artifacts\build-bin\Debug\net8.0\AdminApp\package.json" />
<_ContentIncludedByDefault Remove="artifacts\build-bin\Debug\net8.0\artifacts\test-bin\Debug\net8.0\AdminApp\package.json" />
<_ContentIncludedByDefault Remove="artifacts\build-bin\Debug\net8.0\artifacts\test-bin\Debug\net8.0\artifacts\build-bin\..." />
<!-- ... tiếp tục đệ quy ~80 entries -->
```

Root cause: `artifacts/` directory không được exclude đúng → SDK auto-include files → workaround thêm Remove entries → artifacts chứa output của chính output → đệ quy.

---

## Mục tiêu

1. Xóa toàn bộ `_ContentIncludedByDefault Remove` entries thừa
2. Thêm `DefaultItemExcludes` hoặc sửa `.gitignore` / `Directory.Build.props` để prevent tái phát sinh
3. Verify build vẫn pass sau cleanup

---

## Root Cause Analysis

Build SDK mặc định include tất cả files trong project directory trừ những gì bị exclude. `artifacts/` chứa build output → SDK lại include nó → tạo recursive structure.

Có 2 fix approach:

**Option A**: Thêm `DefaultItemExcludes` trong csproj:
```xml
<PropertyGroup>
    <DefaultItemExcludes>$(DefaultItemExcludes);artifacts\**</DefaultItemExcludes>
</PropertyGroup>
```

**Option B**: Tạo `artifacts/.gitignore` hoặc dùng `Directory.Build.props` ở level solution:
```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <DefaultItemExcludes>$(DefaultItemExcludes);**/artifacts/**</DefaultItemExcludes>
  </PropertyGroup>
</Project>
```

**Khuyến nghị**: Option A đơn giản hơn, scoped cho project này.

---

## Files cần sửa

- `src/MessengerWebhook/MessengerWebhook.csproj` — xóa ~80 Remove entries, thêm DefaultItemExcludes

---

## Implementation Steps

### Step 1: Backup current csproj (0.1 ngày)

```bash
cp src/MessengerWebhook/MessengerWebhook.csproj src/MessengerWebhook/MessengerWebhook.csproj.bak
```

### Step 2: Sửa csproj (0.2 ngày)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>46adf39e-6dd4-48d0-b62d-0101532957ad</UserSecretsId>
    <!-- Prevent build artifacts from being included as content -->
    <DefaultItemExcludes>$(DefaultItemExcludes);artifacts\**;**/node_modules/**</DefaultItemExcludes>
  </PropertyGroup>

  <!-- Xóa toàn bộ ItemGroup chứa _ContentIncludedByDefault Remove entries -->
  
  <ItemGroup>
    <InternalsVisibleTo Include="MessengerWebhook.UnitTests" />
    <InternalsVisibleTo Include="MessengerWebhook.IntegrationTests" />
  </ItemGroup>
  
  <!-- ... rest của csproj giữ nguyên -->

</Project>
```

### Step 3: Verify (0.2 ngày)

```bash
dotnet clean
dotnet build
dotnet test tests/MessengerWebhook.UnitTests --no-build
```

Kiểm tra:
- `artifacts/` không xuất hiện trong build output warnings
- AdminApp assets (`package.json`, `tsconfig.json`) không bị include
- Build warnings count giảm

### Step 4: Xóa backup nếu pass (0.0 ngày)

```bash
rm src/MessengerWebhook/MessengerWebhook.csproj.bak
```

---

## Todo

- [ ] Đọc csproj hiện tại, xác nhận số entries cần xóa
- [ ] Thêm DefaultItemExcludes cho artifacts\**
- [ ] Xóa toàn bộ ItemGroup `_ContentIncludedByDefault Remove`
- [ ] `dotnet clean && dotnet build` — 0 error
- [ ] `dotnet test` — pass
- [ ] Kiểm tra không còn warnings về artifacts

---

## Success Criteria

- csproj không còn `_ContentIncludedByDefault` entries
- `dotnet build` 0 error, không có content-include warnings liên quan artifacts
- File size của csproj giảm đáng kể (từ ~130 dòng xuống ~50 dòng)

---

## Risk

- **AdminApp assets bị miss**: Nếu `package.json` và `tsconfig.json` của AdminApp cần được copy sang output → verify `None Update="Prompts\**\*"` vẫn hoạt động, AdminApp có rule copy riêng
- **Tái phát sinh**: Nếu developer chạy build mà không có DefaultItemExcludes → entries lại xuất hiện. Fix bằng DefaultItemExcludes là permanent solution
