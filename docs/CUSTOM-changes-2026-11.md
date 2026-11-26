# Claim Description, Unique Constraints & Seed Upsert

Hướng dẫn cho dev làm việc trên custom fork này: mô tả claim là gì, DB ràng buộc thế nào, cách seed role/claims từ `identity-data.json`, và các lỗi thường gặp.

---

## 1. Claim Description

Mỗi claim (Client / Role / User) có thêm cột **`Description`** — mô tả human-readable (VD: `"List Backups — ... GET backup"`).

### Ở đâu

| Layer     | File                                                                                         |
| --------- | -------------------------------------------------------------------------------------------- |
| Entity    | `UserIdentityClaim`, `UserIdentityRoleClaim`, `AdminClientClaim` (TPH → bảng `ClientClaims`) |
| Interface | `IDescribableClaim` (`EntityFramework.Extensions/Common`)                                    |
| DTO       | `ClientClaimDto`, `RoleClaimDto`, `UserClaimDto` + `*ApiDto` tương ứng                       |
| UI        | `ClaimsForm`, `ClaimsModal`, tab Role/User Claims                                            |
| Seed JSON | field `Description` bên cạnh `Type` / `Value`                                                |

### Quy tắc bắt buộc khi code claim

**Không** dùng `UserManager.AddClaimAsync` / `RoleManager.AddClaimAsync` nếu cần lưu Description — 2 method đó **bỏ qua** Description.

Phải dùng repository:

```csharp
// Create — thêm entity trực tiếp
await IdentityRepository.CreateUserClaimsAsync(claim);
await IdentityRepository.CreateRoleClaimsAsync(claim);

// Update — phải set ClaimId, update in-place (giữ id ổn định)
await IdentityRepository.UpdateUserClaimsAsync(claim);
await IdentityRepository.UpdateRoleClaimsAsync(claim);
```

---

## 2. Unique constraint trong DB

### Ràng buộc

| Bảng           | Unique index                      | Ý nghĩa                                       |
| -------------- | --------------------------------- | --------------------------------------------- |
| `UserClaims`   | `(UserId, ClaimType, ClaimValue)` | 1 user không được có 2 claim trùng type+value |
| `RoleClaims`   | `(RoleId, ClaimType, ClaimValue)` | tương tự cho role                             |
| `ClientClaims` | `(ClientId, Type, Value)`         | có sẵn từ upstream                            |

- Vẫn **cho phép** cùng `Type` nhưng `Value` khác.
- Rows có `ClaimType`/`ClaimValue` NULL bị loại khỏi index (filter `IS NOT NULL`).

### Giới hạn kiểu dữ liệu (SQL Server)

| Cột          | Max length |
| ------------ | ---------- |
| `ClaimType`  | **256**    |
| `ClaimValue` | **128**    |

Lý do: SQL Server không index được `nvarchar(max)`, và tổng key phải ≤ 1700 bytes (UserId `nvarchar(450)` + type + value).

→ **Claim type > 256 hoặc value > 128 sẽ bị cắt / migration fail.**

### Trước khi chạy migration trên DB có data

```sql
-- 1. Tìm duplicate
SELECT UserId, ClaimType, ClaimValue, COUNT(*)
FROM UserClaims
GROUP BY UserId, ClaimType, ClaimValue
HAVING COUNT(*) > 1;

-- (lặp lại với RoleClaims, cột RoleId)

-- 2. Xóa bớt (giữ id nhỏ nhất hoặc id mong muốn)
-- 3. Cắt claim quá dài
SELECT Id FROM UserClaims WHERE LEN(ClaimValue) > 128 OR LEN(ClaimType) > 256;
```

Rồi mới:

```bash
dotnet ef database update -c AdminIdentityDbContext -p src/Skoruba.Duende.IdentityServer.Admin.EntityFramework.SqlServer
```

### PostgreSQL

Migration unique index **chưa có** cho PostgreSQL. Nếu switch provider phải tạo migration tương tự (alter column + create unique index).

---

## 3. Seed roles + claims từ `identity-data.json`

### Cấu trúc file

```json
{
  "IdentityData": {
    "Roles": [
      {
        "Name": "myapp:admin",
        "Claims": [
          {
            "Type": "backup",
            "Value": "create",
            "Description": "Create Backup — POST backup"
          }
        ]
      }
    ]
  }
}
```

Lưu ý: seed dùng `Type` / `Value` — **không phải** `ClaimType` / `ClaimValue` như API claim thường.

### API upsert

```
POST /api/Roles/UpsertClaims
Authorization: Bearer <token>
Content-Type: application/json
```

Body: dán nguyên nội dung `identity-data.json` (hoặc chỉ `{ "Roles": [...] }`).

**Logic:**

| Tình huống                                  | Hành động                         |
| ------------------------------------------- | --------------------------------- |
| Role chưa có                                | Tạo role                          |
| Claim `(Type, Value)` chưa có               | Tạo claim + Description           |
| Claim đã có, Description khác               | Update Description                |
| Claim đã có, Description giống              | Bỏ qua                            |
| Claim trong DB nhưng không có trong payload | **Không xóa** (chỉ insert/update) |

**Response:**

```json
{ "createdRoles": 0, "createdClaims": 3, "updatedClaims": 12 }
```

### Auth (client credentials)

Đã seed client:

| Field          | Value                                        |
| -------------- | -------------------------------------------- |
| `ClientId`     | `identity_data_pusher`                       |
| `ClientSecret` | `identity_data_pusher_secret`                |
| Grant          | `client_credentials`                         |
| Scope          | `skoruba_identity_admin_api`                 |
| Claim          | `role` = `SkorubaIdentityAdminAdministrator` |

Ví dụ lấy token rồi push:

```bash
# 1. Lấy token
TOKEN=$(curl -s -X POST https://localhost:44310/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=identity_data_pusher" \
  -d "client_secret=identity_data_pusher_secret" \
  -d "scope=skoruba_identity_admin_api" \
  | jq -r .access_token)

# 2. Push
curl -s -X POST https://localhost:44302/api/Roles/UpsertClaims \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @identity-data.json
```

Seed client chỉ chạy khi **client chưa tồn tại** trong DB. Nếu đã có, sửa claims qua **Admin UI → Clients**.

---

## 4. Lỗi thường gặp

### `400 unauthorized_client`

- Client không có grant `client_credentials`, sai `client_id`/`client_secret`, hoặc scope không khớp.
- Check: Admin → Clients → `identity_data_pusher` → Grant Types / Secrets / Allowed Scopes.

### `403 Forbidden` (đã lấy token OK)

Policy Admin bắt **cả 2**:

1. Claim `role` = `SkorubaIdentityAdminAdministrator` (hoặc `client_role` = …)
2. Claim `scope` = `skoruba_identity_admin_api`

Client credentials mặc định **không có role claim** → 403.

**Fix:** Client → Claims → thêm:

| Type   | Value                               |
| ------ | ----------------------------------- |
| `role` | `SkorubaIdentityAdminAdministrator` |

(IdentityServer prefix thành `client_role` khi phát token — đúng policy.)

### `2601 Cannot insert duplicate key ... IX_RoleClaims_RoleId_ClaimType_ClaimValue`

- Push lần 2 mà claim cũ không được nhận ra.
- Nguyên nhân hay gặp: `PageBy` **cap pageSize = 100** — load thiếu claim → cứ `Create`.
- Fix: upsert controller đã page-through hết claims (`while` loop tới `TotalCount`). **Đừng xóa loop này.**
- Hoặc DB đã có data trùng từ trước khi gắn unique index → dọn data (mục 2).

### Claim bị mất Description

Đang dùng `*Manager.AddClaimAsync` → chuyển sang repository (mục 1).

### Migration fail vì data quá dài / trùng

Xem mục 2 — dọn trước khi `database update`.

---

## 5. Thêm claim mới (checklist)

1. Thêm `{ Type, Value, Description }` vào `identity-data.json` (hoặc qua UI Admin).
2. Nếu seed từ script/project khác → chạy generate rồi `--push` / curl ở mục 3.
3. Không trùng `(Type, Value)` trong cùng role (DB chặn).
4. `Description` ≤ reasonable (UI hiển thị); `Value` ≤ 128 chars, `Type` ≤ 256 chars.
5. Claim xuất hiện ở app client qua `/connect/userinfo` (xem `ClaimsProfileService`) — không phải qua access_token, trừ claim chuẩn (`sub`, `role`, `email`…).

---

## 6. ClaimsProfileService (STS) — tóm tắt

Custom `IProfileService` trong STS:

- **Token** (id/access): chỉ claim chuẩn — `sub`, `preferred_username`, `name`, `email`, `email_verified`, `role`.
- **`/connect/userinfo`**: thêm field `claims` = JSON array `{ claimType, claimValue }` gộp user claims + role claims.

Nếu app client cần claim động (VD `backup:create`) → đọc từ **userinfo**, không mong chờ trong JWT.
