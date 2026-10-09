# Môi trường DEV bằng Docker Compose

Dựng nền tảng GĐ0 trên **một máy Linux được chỉ định** để kiểm thử tích hợp thật: PostgreSQL (migration + RLS), RabbitMQ (outbox/inbox), JWT giữa các service qua JWKS, và gateway định tuyến theo host.
Production vẫn chạy trên Kubernetes ([docs 06](../../backend/docs/microservice/06-nen-tang-k8s.md)).

| Container | Vai trò | Cổng ra ngoài |
|---|---|---|
| `gateway` | YARP, điểm vào duy nhất | `GATEWAY_PORT` (mặc định 8080) |
| `identity` | OpenIddict, tài khoản, quyền | — |
| `tenant` | Đơn vị, license, provisioning | — |
| `postgres` | DB `elib_identity`, `elib_tenant`, mỗi DB một role riêng | `127.0.0.1:5432` |
| `rabbitmq` | Message bus | UI `127.0.0.1:15672` |

## Yêu cầu

- **Máy đích:** Linux có Docker Engine ≥ 24 với compose plugin; user SSH thuộc nhóm `docker`; RAM trống ≥ 3 GB (build .NET tốn RAM nhất).
- **Máy chạy lệnh:** chỉ cần `ssh` và `tar`, Git Bash trên Windows là đủ. Không cần cài Docker.
- **Tên miền wildcard** trỏ về máy đích, vì gateway xác định đơn vị theo subdomain. Nếu không có DNS riêng, dùng `<IP>.sslip.io`: mọi tên `*.<IP>.sslip.io` đều phân giải về IP đó.

## Triển khai

```sh
# 1. Đẩy mã nguồn lên máy đích. Lần đầu, script dừng lại vì máy đích chưa có .env.
./deploy/dev/deploy.sh dev@10.0.0.5            # SSH cổng/khoá khác: ELIB_SSH_OPTS="-p 2222 -i ~/.ssh/elib"

# 2. Sinh secret NGAY TRÊN máy đích. Secret không đi qua máy chạy lệnh.
ssh dev@10.0.0.5 'cd elib-dev/deploy/dev && sh init-env.sh 10.0.0.5.sslip.io 8080'

# 3. Build image trên máy đích và khởi động.
./deploy/dev/deploy.sh dev@10.0.0.5

# 4. Kiểm tra nhanh.
ssh dev@10.0.0.5 'sh elib-dev/deploy/dev/smoke.sh'
```

Những lần cập nhật sau chỉ cần chạy lại bước 3.

`smoke.sh` kiểm tra:
- gateway sống;
- OIDC discovery đi qua gateway;
- `client_credentials` cấp được token;
- trang đăng nhập mở được;
- gateway gọi được tenant bằng service token;
- role DB không phải superuser và không có `BYPASSRLS`;
- các bảng có `FORCE RLS`;
- migration đã chạy;
- queue RabbitMQ đã khai báo.

## Địa chỉ

Ví dụ với `DEV_DOMAIN=10.0.0.5.sslip.io`, cổng 8080:

| Địa chỉ | Dùng cho |
|---|---|
| `http://id.10.0.0.5.sslip.io:8080/.well-known/openid-configuration` | OIDC discovery, issuer |
| `http://quantri.10.0.0.5.sslip.io:8080/api/system/...` | API quản trị nền tảng: không gắn đơn vị, chỉ token hệ thống |
| `http://<subdomain>.10.0.0.5.sslip.io:8080/api/...` | API của một đơn vị |

Tài khoản quản trị hệ thống đầu tiên là `sysadmin`, mật khẩu là `BOOTSTRAP_ADMIN_PASSWORD` trong `.env` trên máy đích. Tài khoản này chỉ được tạo khi DB còn trống.

## Tên miền thật (sau nginx + TLS)

Máy DEV hiện tại dùng `elib-k12-micro.thuvientn.vn`:

```
Internet ──HTTPS──▶ nginx của máy (certbot) ──HTTP──▶ 127.0.0.1:10680 gateway ──▶ identity / tenant
```

1. Trong `.env` trên máy đích, đặt:

   ```
   DEV_DOMAIN=elib-k12-micro.thuvientn.vn
   IDENTITY_ISSUER=https://elib-k12-micro.thuvientn.vn/
   GATEWAY_BIND=127.0.0.1
   ```

   - Tên gốc được dùng làm host hệ thống: đăng nhập và quản trị nền tảng.
   - Gateway chỉ nghe loopback, nên từ ngoài phải đi qua nginx.
2. Cài server block nginx và xin chứng chỉ:
   1. `sudo cp nginx/elib-k12-micro.conf /etc/nginx/conf.d/`
   2. `sudo nginx -t && sudo systemctl reload nginx`
   3. `sudo certbot --nginx -d elib-k12-micro.thuvientn.vn --redirect`
3. Gateway chỉ tin `X-Forwarded-For/Proto` đến từ dải bridge của docker (`Gateway__TrustedProxyNetworks`). Nhờ vậy:
   - rate limit tính theo IP thật của từng người dùng;
   - identity sinh URL `https`;
   - client gọi thẳng vào gateway không giả được IP.

**Subdomain đơn vị** (`<truong>.elib-k12-micro.thuvientn.vn`) cần thêm hai thứ:
- bản ghi DNS wildcard `*.elib-k12-micro.thuvientn.vn` trỏ về máy;
- chứng chỉ wildcard. Chứng chỉ này phải xin bằng DNS-01, ví dụ `certbot certonly --manual --preferred-challenges dns -d '*.elib-k12-micro.thuvientn.vn'`.

Chứng chỉ hiện tại chỉ cấp cho tên gốc.

## App Admin và kịch bản GĐ0

- App Admin (Angular, `src/Web/projects/admin`) được phục vụ tại `/admin/` trên mọi host:
  - **host hệ thống:** quản trị nền tảng: đơn vị, license, quản trị viên đầu tiên;
  - **host đơn vị:** người dùng, vai trò, menu theo phân hệ đã mua.
- App đăng nhập bằng OIDC code + PKCE. Client `elib-admin` nhận:
  - redirect URI cố định cho host hệ thống (`PUBLIC_ORIGIN`);
  - mẫu `*.<miền>` cho host đơn vị (`PUBLIC_TENANT_ORIGIN_PATTERN`).
- `python3 e2e_gd0.py` (chạy trên máy đích) đi hết tiêu chí GĐ0 theo đúng luồng của app:
  1. sysadmin đăng nhập;
  2. tạo đơn vị `TH-DEMO`; saga khởi tạo chạy đến khi đơn vị Active;
  3. tạo quản trị viên đầu tiên;
  4. quản trị viên đăng nhập trên host đơn vị và đổi mật khẩu bắt buộc;
  5. kiểm tra menu theo license và tính cô lập.

  Mật khẩu tài khoản demo nằm trong `demo-accounts.txt` (quyền 600, không commit).

## Khác với production (chủ ý)

| Hạng mục | DEV | Production |
|---|---|---|
| Khoá ký token | Sinh mới mỗi lần identity khởi động. Restart identity thì token cũ hết hiệu lực. | Chứng chỉ trong secret, xoay theo lịch |
| HTTPS | Tắt (`Identity__RequireHttps=false`) | Bắt buộc, TLS ở ingress |
| Migration | Service tự chạy khi khởi động (`Database__MigrateOnStartup`) | Kubernetes Job chạy trước khi rollout |
| Secret | `.env` quyền 600 trên máy đích | External Secrets / Sealed Secrets |

## Lưu ý

- **Không chạy service bằng user `postgres`.** Superuser luôn bỏ qua RLS. Script init tạo role `*_app` với `NOSUPERUSER NOBYPASSRLS`, và `smoke.sh` kiểm tra điều này.
- **Đổi secret DB sau khi đã khởi tạo** thì phải xoá volume: `docker compose down -v`. Lệnh này mất toàn bộ dữ liệu dev, sau đó chạy lại `init-env.sh`.
- **Xem log:** `docker compose logs -f identity tenant gateway`, chạy trong `elib-dev/deploy/dev` trên máy đích.
- **Vào Postgres/RabbitMQ từ máy mình:** mở SSH tunnel, ví dụ `ssh -L 5432:127.0.0.1:5432 -L 15672:127.0.0.1:15672 dev@10.0.0.5`.
