# Nhân bản dữ liệu template cho 92 đơn vị phường/xã

## Context

94 Tenant đã seed (Id 1-94). Cần nhân bản dữ liệu cấu hình mẫu từ PDP (Id=1) sang 91 phường/xã còn lại (Id 2-92). SVH (Id=93) và TVT (Id=94) không nhân bản. Dữ liệu Ebook dùng chung set TenantId=NULL.

**Dữ liệu mẫu hiện tại:**
- TenantId=1 (PDP): SystemParameter(25), MenuType(1), Menu(16)
- TenantId=53 (orphan từ seed cũ): LinkGroup(1), Link(5), ReaderType(9), DigType(13), MetadataSchemaRegistry(2), MetaDataFieldRegistery(128)

## Output

**File mới**: `scripts/seed-clone-data.py` — Python script chạy trên cả SQL Server (pyodbc) và PostgreSQL (psycopg2)

## Phase 0 — Gộp template về PDP

Chuyển data TenantId=53 về đúng chỗ:

| Bảng | Hành động | Lý do |
|------|-----------|-------|
| cms.LinkGroup, cms.Link, dbo.ReaderType | UPDATE TenantId=53 → 1 | Per-tenant, gộp về PDP template |
| Ebook.DigType, Ebook.MetadataSchemaRegistry, Ebook.MetaDataFieldRegistery | UPDATE TenantId=53 → NULL | Dùng chung, tất cả tenant thấy |

Sau phase 0, PDP có: 25 SystemParameter + 1 MenuType + 16 Menu + 1 LinkGroup + 5 Link + 9 ReaderType = **57 rows template**

## Phase 1 — Đọc template từ PDP (TenantId=1)

Đọc 57 rows template từ SQL Server, lưu vào Python dict. Giữ lại Id gốc cho MenuType, Menu, LinkGroup, Link (cần remap FK).

**Dữ liệu Menu (16 rows, 2 cấp):**
- 5 root (ParentId=0): Trang chủ, Giới thiệu, Tin tức, Tìm kiếm, Hướng dẫn-Bạn đọc
- 11 children: link đến Category PublicId, /search, /advanced-search

## Phase 2 — Nhân bản cho 91 tenant (Id 2→92)

Thứ tự INSERT quan trọng vì FK:

```
1. SystemParameter (25 rows × 91)  — no FK
2. ReaderType      (9 rows × 91)   — no FK
3. MenuType        (1 row × 91)    — no FK, nhưng cần capture new Id → menu_type_map
4. LinkGroup       (1 row × 91)    — no FK, nhưng cần capture new Id → link_group_map
5. Menu roots      (5 rows × 91)   — ParentId=0, remap MenuType FK → menu_map
6. Menu children   (11 rows × 91)  — remap ParentId + MenuType FK
7. Link            (5 rows × 91)   — remap LinkGroupId FK
```

**FK remapping strategy (trong Python):**

| FK | Cách xử lý |
|----|-----------|
| Menu.MenuType → MenuType.Id | INSERT MenuType với OUTPUT/RETURNING lấy new Id, tạo dict `{old_id: new_id}` per tenant |
| Menu.ParentId → Menu.Id (self-ref) | 2-pass: INSERT root (ParentId=0) trước, capture mapping, rồi INSERT children với remapped ParentId |
| Link.LinkGroupId → LinkGroup.Id | INSERT LinkGroup với OUTPUT/RETURNING, tạo dict mapping |

**SQL dialect differences:**

| Aspect | SQL Server | PostgreSQL |
|--------|-----------|-----------|
| Schema dbo | `dbo` | `public` |
| Schema Ebook | `Ebook` | `ebook` |
| Table/column quotes | Không cần | `"PascalCase"` bắt buộc |
| Get new Id | `OUTPUT INSERTED.Id` | `RETURNING "Id"` |
| UUID | `NEWID()` | `gen_random_uuid()::varchar` |
| Timestamp | `GETDATE()` | `NOW()` |
| systemparameter | `dbo.systemparameter` | `public."systemparameter"` (lowercase) |
| Link column name | `Link` (column=table name) | `"Link"` |

## Phase 3 — Verify

**Expected row counts sau khi hoàn thành:**

| Bảng | Count |
|------|-------|
| SystemParameter | 25 + (25×91) = 2,300 |
| MenuType | 1 + 91 = 92 |
| Menu | 16 + (16×91) = 1,472 |
| LinkGroup | 1 + 91 = 92 |
| Link | 5 + (5×91) = 460 |
| ReaderType | 9 + (9×91) = 828 |
| DigType | 13 (TenantId=NULL) |
| MetadataSchemaRegistry | 2 (TenantId=NULL) |
| MetaDataFieldRegistery | 128 (TenantId=NULL) |

**Tổng rows mới per DB**: 57 × 91 = 5,187

**Kiểm tra:**
- COUNT(*) mỗi bảng trên cả 2 DB
- TenantId=93 và TenantId=94 phải có 0 rows
- Sample 1 tenant ngẫu nhiên (VD Id=50) kiểm tra đủ 57 rows

## Script structure

```
scripts/seed-clone-data.py
├── DB adapter (SQL Server pyodbc / PostgreSQL psycopg2)
│   ├── insert_returning_id() — INSERT + OUTPUT/RETURNING
│   ├── insert_batch() — INSERT many rows (no FK tables)
│   └── update() / select() / count()
├── phase0_consolidate() — UPDATE TenantId orphaned data
├── phase1_read_template() — Read 57 rows from TenantId=1
├── phase2_clone_tenant(tid) — Clone with FK remapping per tenant
│   ├── Insert SystemParameter, ReaderType (batch, no FK)
│   ├── Insert MenuType → capture id map
│   ├── Insert LinkGroup → capture id map
│   ├── Insert Menu roots → capture id map
│   ├── Insert Menu children → remap ParentId + MenuType
│   └── Insert Link → remap LinkGroupId
├── main loop — for tid in range(2, 93)
└── phase3_verify() — COUNT checks + sample output

Dependencies: pip install pyodbc psycopg2-binary
```

## Critical files

- `ELIBAPI.Core/Entities/Cms/Menu.cs` — self-ref ParentId, MenuType FK
- `ELIBAPI.Core/Entities/Cms/Link.cs` — `[Column("Link")]` mapping
- `ELIBAPI.Infrastructure/Data/ELIBAPIDbContext.cs` — schema mapping (line 217: dbo→public cho PG)
- `scripts/seed-pg.py` — pattern tham khảo cho psycopg2 usage
