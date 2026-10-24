#!/usr/bin/env python3
"""
Kiểm tra tiêu chí hoàn thành GĐ0 trên môi trường DEV, đi đúng luồng của app Admin (OIDC code + PKCE):
  1. sysadmin đăng nhập trên host hệ thống (URL công khai, qua nginx TLS)
  2. tạo đơn vị + bán phân hệ → saga khởi tạo (tenant → RabbitMQ → identity → TenantSeeded) → Active
  3. tạo quản trị viên đầu tiên cho đơn vị
  4. quản trị viên đăng nhập trên host của đơn vị (redirect URI theo mẫu *.<miền>), đổi mật khẩu bắt buộc,
     thấy đúng phân hệ đã mua, gọi được API người dùng.
  5. cô lập giữa host hệ thống và host đơn vị.
  6. danh mục mặc định của đơn vị, sửa tham số, OPAC ẩn danh chỉ đọc được tham số công khai.
  7. notification: mẫu email mặc định, gửi thư kiểm tra qua SMTP nền tảng (Mailpit bắt thư), nhật ký gửi, chặn SMTP nội bộ.
  8. phân quyền: danh mục quyền, vai trò mới với quyền theo module, mã quyền lạ bị từ chối.
  9. OTP (mã lấy từ Mailpit) và CAPTCHA khi đăng nhập, bật/tắt bằng tham số của đơn vị (cache xoá qua SystemParameterChanged).
 10. quản trị nền tảng đóng vai đơn vị: vé dùng một lần → app trên host đơn vị → chỉ đọc (cần mật khẩu sysadmin).
 11. audit: nhật ký của đơn vị có đăng nhập, đổi vai trò, sửa tham số (đi qua outbox → RabbitMQ → service audit);
     nhật ký nền tảng có đồng bộ đơn vị + vào xem đơn vị (cần mật khẩu sysadmin).
 12. media: upload qua URL ký (PUT thẳng vào MinIO qua gateway /s3), kiểm tra magic bytes, tải lại qua URL ký;
     logo đơn vị trên bucket công khai + manifest.json (phần logo cần mật khẩu sysadmin).
 13. nhập danh mục từ Excel: file mẫu, file có dòng trùng bị từ chối cả file (báo số dòng), bỏ qua dòng trùng, nhật ký IMPORT.
 14. OPAC ở gốc host đơn vị; host hệ thống "/" → app Admin.
 15. bạn đọc (patron): danh mục, thêm, trùng số thẻ, tìm, khoá/mở thẻ, sửa hàng loạt, nhập Excel, nhật ký,
     ảnh thẻ (media, riêng tư) + gán ảnh theo số thẻ, xuất Excel theo bộ lọc.
 16. biên mục (catalog): loại biểu ghi + biểu mẫu mặc định (tự dựng bản sao đơn vị khi service mới triển khai), từ điển MARC21,
     biên mục biểu ghi theo biểu mẫu (001/003/005/008 tự sinh), tìm không dấu, trùng ISBN, ẩn khỏi OPAC, nhật ký,
     xuất ISO2709 + nhập lại (bỏ qua trùng), nhập MARCXML (biểu ghi lỗi: từ chối cả file / bỏ qua), xoá.
 17. kho (holdings): loại kho + kho chung mặc định, đăng ký ĐKCB theo lô (nhan đề lấy từ catalog), số tiếp theo, trùng mã,
     xếp giá bằng mã quét, tìm theo trạng thái, kho còn sách không xoá được, nhật ký, dọn.
 18. lưu thông (circulation): quầy + chính sách mặc định, thông tin bạn đọc ở quầy, mượn (bản chưa xếp giá bị từ chối),
     không mượn trùng, gia hạn, trả bằng mã quét, lịch sử, nhật ký.
 19. lưu thông (tiếp): đặt mượn — hết bản thì xếp hàng, trả sách thì giữ cho người đặt + email "đã sẵn sàng" (Mailpit),
     người khác không mượn được bản đang giữ; phiếu phạt báo mất → lượt mượn đóng, holdings chuyển bản sách sang "Mất";
     sao chụp; báo cáo hoạt động + tài liệu mất; xuất Excel lịch sử.
 20. tra cứu OPAC (search): chỉ mục tự dựng khi service mới triển khai, tìm không dấu ẩn danh theo host, tìm nâng cao theo tác giả,
     số bản / bản sẵn sàng, chi tiết + tình trạng từng bản, gợi ý nhan đề, ẩn biểu ghi khỏi OPAC, quản trị chỉ mục (trạng thái, dựng lại).
 21. bổ sung (acquisition): đơn đặt (tỷ giá, thanh toán), phiếu nhận từ đơn đặt, khoá số lượng ĐKCB theo đơn nhận, hoàn tất khoá phiếu,
     giao nhận theo đơn nhận, nhật ký holdings ghi đúng cán bộ, báo cáo bổ sung + tra cứu đơn nhận (xuất Excel), báo cáo kho theo ngày nhận.
 22. OPAC mở rộng: xem MARC/ISBD + tải .mrc ẩn danh, ảnh bìa (upload media + tra theo ISBN), thống kê tra cứu + lượt mở kết quả,
     Z39.50 ra Library of Congress (lx2.loc.gov:210/LCDB — máy chủ ngoài, lỗi mạng chỉ cảnh báo), chặn máy chủ trong mạng nội bộ,
     OPAC chỉ thấy máy chủ được bật, giới hạn lượt tra cứu theo IP (429 + Retry-After — chạy cuối vì chặn OPAC của IP này ~1 phút).
Chạy trên máy DEV: python3 e2e_gd0.py   (đọc secret từ .env cùng thư mục; mật khẩu tài khoản demo ghi vào demo-accounts.txt, quyền 600)
  - sysadmin đã đổi mật khẩu bắt buộc: ELIB_SYSADMIN_PASSWORD='...' python3 e2e_gd0.py
  - không có mật khẩu sysadmin: python3 e2e_gd0.py --tenant-only  (bỏ bước 1–3 và phần cần quản trị nền tảng)
Bước 4 gọi gateway qua 127.0.0.1:<GATEWAY_PORT> với Host của đơn vị (chưa có DNS wildcard), coi như đi qua nginx https.
"""
import base64, hashlib, html, http.cookiejar, io, json, os, re, secrets, socket, sys, time, urllib.error, urllib.parse, urllib.request, uuid, zipfile
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
ENV = dict(l.strip().split("=", 1) for l in open(os.path.join(HERE, ".env"), encoding="utf-8") if "=" in l and not l.startswith("#"))
DOMAIN = ENV["DEV_DOMAIN"]
PUBLIC = ENV.get("PUBLIC_ORIGIN") or f"http://{DOMAIN}:{ENV.get('GATEWAY_PORT', '8080')}"
GATEWAY = f"http://127.0.0.1:{ENV.get('GATEWAY_PORT', '8080')}"
TENANT_CODE, SUBDOMAIN = "TH-DEMO", "th-demo"
TENANT_ONLY = "--tenant-only" in sys.argv


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


class Browser:
    """Trình duyệt tối giản: giữ cookie, KHÔNG tự theo redirect. base = URL thật; host = Host header (nếu đi vòng qua gateway)."""

    def __init__(self, base, host=None, proto=None):
        self.base, self.host, self.proto = base, host, proto
        policy = http.cookiejar.DefaultCookiePolicy()
        if proto == "https":
            # Đi vòng http://127.0.0.1 nhưng đóng vai nginx https: vẫn gửi cookie Secure như trình duyệt trên https.
            policy.return_ok_secure = lambda cookie, request: True
        self.jar = http.cookiejar.CookieJar(policy)
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect)

    def request(self, method, path, data=None, token=None, json_body=None, raw=None, binary=False, headers=None):
        headers = dict(headers or {})
        if self.host: headers["Host"] = self.host
        if self.proto: headers["X-Forwarded-Proto"] = self.proto
        if token: headers["Authorization"] = "Bearer " + token
        body = None
        if raw is not None:
            body, ctype = raw; headers["Content-Type"] = ctype
        elif json_body is not None:
            body = json.dumps(json_body).encode(); headers["Content-Type"] = "application/json"
        elif data is not None:
            body = urllib.parse.urlencode(data).encode(); headers["Content-Type"] = "application/x-www-form-urlencoded"
        req = urllib.request.Request(self.base + path, data=body, method=method, headers=headers)
        for attempt in range(3):
            try:
                with self.opener.open(req, timeout=30) as r:
                    content = r.read()
                    return r.status, dict(r.headers), content if binary else content.decode()
            except urllib.error.HTTPError as e:
                return e.code, dict(e.headers), e.read().decode(errors="replace")
            except urllib.error.URLError as e:
                # Chỉ lỗi MỞ KẾT NỐI (request chưa gửi) — máy tự gọi tên miền công khai của mình qua NAT đôi khi chập chờn.
                if attempt == 2 or not isinstance(e.reason, (TimeoutError, ConnectionRefusedError, OSError)): raise
                time.sleep(2)


def step(msg): print("  [OK]  ", msg)


def fail(msg):
    print("  [FAIL]", msg); sys.exit(1)


def expect(cond, msg, detail=""):
    step(msg) if cond else fail(f"{msg} — {detail[:300]}")


def path_of(location):
    u = urllib.parse.urlsplit(location)
    return u.path + ("?" + u.query if u.query else "")


def pkce_login(b, origin, user, password, tenant_hint=None, otp_code=None):
    """Luồng của app Admin: authorize → trang đăng nhập → authorize → callback?code → token."""
    verifier = base64.urlsafe_b64encode(secrets.token_bytes(32)).rstrip(b"=").decode()
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
    redirect = origin + "/admin/callback"
    q = {"client_id": "elib-admin", "response_type": "code", "redirect_uri": redirect, "scope": "openid profile offline_access elib-api",
         "code_challenge": challenge, "code_challenge_method": "S256", "state": "e2e"}
    s, h, body = b.request("GET", "/connect/authorize?" + urllib.parse.urlencode(q))
    expect(s == 302 and "/account/login" in h.get("Location", ""), "authorize → trang đăng nhập", f"{s} {body}")
    login_path = path_of(h["Location"])
    s, h, page = b.request("GET", login_path)
    token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
    expect(s == 200 and token, "trang đăng nhập có antiforgery", f"{s}")
    return_url = urllib.parse.parse_qs(urllib.parse.urlsplit(login_path).query)["ReturnUrl"][0]
    form = {"__RequestVerificationToken": html.unescape(token.group(1)), "userName": user, "password": password, "returnUrl": return_url}
    if tenant_hint: form["tenant"] = tenant_hint
    s, h, body = b.request("POST", "/account/login", data=form)
    expect(s == 302, f"đăng nhập '{user}'", f"{s} {re.sub('<[^>]+>', ' ', body)}")
    if path_of(h["Location"]) == "/account/otp":
        expect(otp_code is not None, "tài khoản yêu cầu OTP", "")
        s, _, page = b.request("GET", "/account/otp")
        af = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
        expect(s == 200 and af, "trang nhập OTP", f"{s}")
        s, h, body = b.request("POST", "/account/otp", data={"__RequestVerificationToken": html.unescape(af.group(1)), "code": otp_code()})
        expect(s == 302, "nhập mã OTP", f"{s} {re.sub('<[^>]+>', ' ', body)}")
    s, h, body = b.request("GET", path_of(h["Location"]))
    loc = h.get("Location", "")
    expect(s == 302 and loc.startswith(redirect), f"chuyển về redirect URI {redirect}", f"{s} {loc} {body}")
    code = urllib.parse.parse_qs(urllib.parse.urlsplit(loc).query)["code"][0]
    s, h, body = b.request("POST", "/connect/token", data={"grant_type": "authorization_code", "code": code, "redirect_uri": redirect,
                                                          "client_id": "elib-admin", "code_verifier": verifier})
    expect(s == 200, "đổi code lấy token (PKCE)", f"{s} {body}")
    return json.loads(body)["access_token"]


def reader_login(b, origin, card, password, new_password):
    """Luồng của OPAC (client elib-opac): authorize → đăng nhập bạn đọc → đổi mật khẩu lần đầu → callback?code → token."""
    verifier = base64.urlsafe_b64encode(secrets.token_bytes(32)).rstrip(b"=").decode()
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
    redirect = origin + "/tai-khoan/callback"
    q = {"client_id": "elib-opac", "response_type": "code", "redirect_uri": redirect, "scope": "openid profile offline_access elib-api",
         "code_challenge": challenge, "code_challenge_method": "S256", "state": "e2e"}
    s, h, body = b.request("GET", "/connect/authorize?" + urllib.parse.urlencode(q))
    expect(s == 302 and "/account/reader-login" in h.get("Location", ""), "OPAC authorize → trang đăng nhập bạn đọc", f"{s} {body[:200]}")
    login_path = path_of(h["Location"])
    s, h, page = b.request("GET", login_path)
    af = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
    return_url = urllib.parse.parse_qs(urllib.parse.urlsplit(login_path).query)["ReturnUrl"][0]
    s, h, body = b.request("POST", "/account/reader-login", data={"__RequestVerificationToken": html.unescape(af.group(1)), "cardNo": card,
                                                                 "password": password, "returnUrl": return_url})
    expect(s == 302 and path_of(h["Location"]) == "/account/reader-password", f"bạn đọc {card} đăng nhập bằng số thẻ → bắt đổi mật khẩu",
           f"{s} {re.sub('<[^>]+>', ' ', body)[:300]}")
    s, h, page = b.request("GET", "/account/reader-password")
    af = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
    s, h, body = b.request("POST", "/account/reader-password", data={"__RequestVerificationToken": html.unescape(af.group(1)),
                                                                    "newPassword": new_password, "confirmPassword": new_password})
    expect(s == 302, "đổi mật khẩu lần đầu", f"{s} {re.sub('<[^>]+>', ' ', body)[:300]}")
    s, h, body = b.request("GET", path_of(h["Location"]))
    loc = h.get("Location", "")
    expect(s == 302 and loc.startswith(redirect), f"chuyển về OPAC {redirect}", f"{s} {loc} {body[:200]}")
    code = urllib.parse.parse_qs(urllib.parse.urlsplit(loc).query)["code"][0]
    s, h, body = b.request("POST", "/connect/token", data={"grant_type": "authorization_code", "code": code, "redirect_uri": redirect,
                                                          "client_id": "elib-opac", "code_verifier": verifier})
    expect(s == 200, "OPAC đổi code lấy token bạn đọc (PKCE)", f"{s} {body[:200]}")
    return json.loads(body)["access_token"]


def xlsx(rows):
    """File .xlsx tối thiểu (chuỗi inline) — máy DEV không có thư viện Excel cho Python."""
    def col(i): return chr(ord("A") + i)
    sheet = "".join(
        f'<row r="{r + 1}">' + "".join(f'<c r="{col(c)}{r + 1}" t="inlineStr"><is><t>{escape(v)}</t></is></c>' for c, v in enumerate(row) if v is not None) + "</row>"
        for r, row in enumerate(rows))
    ns = 'xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"'
    rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
    files = {
        "[Content_Types].xml": '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/>'
            '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
            '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>',
        "_rels/.rels": f'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            f'<Relationship Id="rId1" Type="{rel}/officeDocument" Target="xl/workbook.xml"/></Relationships>',
        "xl/workbook.xml": f'<?xml version="1.0" encoding="UTF-8"?><workbook {ns}><sheets><sheet name="Data" sheetId="1" r:id="rId1"/></sheets></workbook>',
        "xl/_rels/workbook.xml.rels": f'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            f'<Relationship Id="rId1" Type="{rel}/worksheet" Target="worksheets/sheet1.xml"/></Relationships>',
        "xl/worksheets/sheet1.xml": f'<?xml version="1.0" encoding="UTF-8"?><worksheet {ns}><sheetData>{sheet}</sheetData></worksheet>',
    }
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        for name, content in files.items(): z.writestr(name, content)
    return buf.getvalue()


class Sip2Kiosk:
    """Trạm SIP2 tối giản (socket thuần, không dùng code C#): thêm AY + AZ checksum, đọc một dòng trả lời, kiểm tra checksum."""

    def __init__(self, port):
        self.sock = socket.create_connection(("127.0.0.1", port), timeout=15)
        self.seq = 0

    def send(self, body):
        self.seq = (self.seq + 1) % 10
        data = f"{body}AY{self.seq}AZ".encode()
        self.sock.sendall(data + ("%04X" % ((-sum(data)) & 0xFFFF)).encode() + b"\r")
        reply = b""
        while not reply.endswith(b"\r"):
            chunk = self.sock.recv(4096)
            if not chunk: break
            reply += chunk
        reply = reply.rstrip(b"\r")
        ok = len(reply) > 6 and ("%04X" % ((-sum(reply[:-4])) & 0xFFFF)) == reply[-4:].decode()
        return reply.decode("utf-8"), ok

    @staticmethod
    def field(reply, tag):
        return next((p[2:] for p in reply.split("|") if p.startswith(tag)), None)

    def close(self):
        self.sock.close()


def multipart_fields(files, fields):
    """multipart nhiều phần: files = [(tên trường, tên file, nội dung, kiểu)], fields = {tên: giá trị chuỗi}."""
    boundary = "e2e" + secrets.token_hex(8)
    body = b""
    for field, filename, content, ctype in files:
        body += (f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="{filename}"\r\nContent-Type: {ctype}\r\n\r\n').encode() + content + b"\r\n"
    for name, value in fields.items():
        body += (f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n').encode()
    return body + f"--{boundary}--\r\n".encode(), f"multipart/form-data; boundary={boundary}"


def multipart(field, filename, content, ctype):
    boundary = "e2e" + secrets.token_hex(8)
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="{filename}"\r\nContent-Type: {ctype}\r\n\r\n').encode() \
        + content + f"\r\n--{boundary}--\r\n".encode()
    return body, f"multipart/form-data; boundary={boundary}"


ACCOUNTS_FILE = os.path.join(HERE, "demo-accounts.txt")


def save(accounts):
    """Ghi NGAY sau mỗi thay đổi mật khẩu — script dừng giữa chừng cũng không làm mất mật khẩu."""
    old = os.umask(0o077)
    with open(ACCOUNTS_FILE, "w", encoding="utf-8") as f:
        f.writelines(f"{k}={v}\n" for k, v in accounts.items())
    os.umask(old)


def main():
    accounts = dict(l.strip().split("=", 1) for l in open(ACCOUNTS_FILE, encoding="utf-8")) if os.path.exists(ACCOUNTS_FILE) else {}
    admin_user = accounts.setdefault("TENANT_ADMIN_USER", "qtdemo")
    sysb = Browser(PUBLIC)
    if TENANT_ONLY:
        print("1–3. [BỎ QUA] --tenant-only: không dùng tài khoản quản trị nền tảng")
        expect("TENANT_ADMIN_PASSWORD" in accounts, f"đã có mật khẩu '{admin_user}' trong {ACCOUNTS_FILE}")
        sys_token, tenant = None, None
    else:
        sys_token, tenant = system_steps(sysb, accounts, admin_user)
    tenant_steps(sysb, sys_token, tenant, accounts, admin_user)
    print(f"TẤT CẢ ĐẠT — tài khoản demo ghi ở {ACCOUNTS_FILE}")


def system_steps(sysb, accounts, admin_user):
    print(f"1. Quản trị nền tảng — {PUBLIC}")
    s, _, body = sysb.request("GET", "/admin/")
    expect(s == 200 and "<app-root>" in body, "tải app Admin")
    sys_token = pkce_login(sysb, PUBLIC, "sysadmin", os.environ.get("ELIB_SYSADMIN_PASSWORD") or ENV["BOOTSTRAP_ADMIN_PASSWORD"])
    s, _, body = sysb.request("GET", "/api/me", token=sys_token)
    me = json.loads(body)
    expect(s == 200 and me["tenantId"] is None, f"/api/me: {me['userName']} (quản trị hệ thống, buộc đổi mật khẩu={me['mustChangePassword']})")

    s, _, body = sysb.request("GET", "/api/system/health", token=sys_token)
    health = json.loads(body) if s == 200 else {}
    expect(s == 200 and health.get("services"), "sức khoẻ nền tảng (gateway ping /ready): " + ", ".join(f"{x['name']}={x['status']}" for x in health.get("services", [])), body[:200])
    s, _, body = sysb.request("POST", "/api/system/digital/stats/digital", token=sys_token, json_body={"tenantIds": [], "from": None, "to": None})
    expect(s == 200 and isinstance(json.loads(body), list), "thống kê tài liệu số toàn hệ thống (sysadmin)", f"{s} {body[:200]}")
    print(f"  [THÔNG TIN]  hàng đợi _error RabbitMQ: {health.get('queues') if health.get('queues') is not None else 'không đọc được — ' + str(health.get('queuesDetail'))}")

    print("2. Tạo đơn vị + khởi tạo")
    s, _, body = sysb.request("GET", "/api/system/tenant/tenants?search=" + TENANT_CODE.lower(), token=sys_token)
    existing = [t for t in json.loads(body)["items"] if t["code"] == TENANT_CODE]
    if existing:
        tenant = existing[0]; step(f"đơn vị {TENANT_CODE} đã có từ lần chạy trước")
    else:
        s, _, body = sysb.request("POST", "/api/system/tenant/tenants", token=sys_token, json_body={
            "code": TENANT_CODE, "name": "Trường demo nền tảng mới", "subdomain": SUBDOMAIN,
            "licenses": [{"moduleCode": m} for m in ["CATALOG", "HOLDINGS", "CIRCULATION", "SEARCH", "ACQUISITION", "PAYMENT", "DIGITAL", "PORTAL", "SERIALS"]]})
        expect(s == 201, f"tạo đơn vị {TENANT_CODE}", body)
        tenant = json.loads(body)
    for _ in range(60):
        s, _, body = sysb.request("GET", f"/api/system/tenant/tenants/{tenant['publicId']}", token=sys_token)
        tenant = json.loads(body)
        if tenant["status"] != "Provisioning": break
        time.sleep(1)
    steps = ", ".join(f"{p['service']}={p['status']}" for p in tenant["provisioningSteps"])
    expect(tenant["status"] == "Active", f"saga khởi tạo xong: Active ({steps})", json.dumps(tenant))
    owned = {l["moduleCode"] for l in tenant["licenses"]}
    missing = [m for m in ["ACQUISITION", "PAYMENT", "DIGITAL", "PORTAL", "SERIALS"] if m not in owned]
    if missing:  # đơn vị từ lần chạy trước: bán thêm các phân hệ mới
        s, _, body = sysb.request("PUT", f"/api/system/tenant/tenants/{tenant['publicId']}/licenses", token=sys_token, json_body={
            "licenses": [{"moduleCode": m} for m in sorted(owned) + missing]})
        expect(s == 200, f"bán thêm phân hệ {', '.join(missing)} cho đơn vị", body)
        tenant = json.loads(body)
    s, _, body = sysb.request("POST", f"/api/system/tenant/tenants/{tenant['publicId']}/resync", token=sys_token)
    expect(s == 200, "đồng bộ lại đơn vị sang các service (bản sao cho service thêm sau)", body)

    print("3. Quản trị viên đầu tiên của đơn vị")
    if "TENANT_ADMIN_PASSWORD" not in accounts and "TENANT_ADMIN_TEMP" not in accounts:
        temp = "Tam" + secrets.token_hex(6) + "7"
        s, _, body = sysb.request("POST", f"/api/system/identity/tenants/{tenant['id']}/admins", token=sys_token,
                                  json_body={"userName": admin_user, "fullName": "Quản trị Trường demo", "email": None, "password": temp})
        expect(s == 201, f"tạo tài khoản '{admin_user}'", body)
        accounts["TENANT_ADMIN_TEMP"] = temp
        accounts["TENANT_ADMIN_URL"] = f"https://{SUBDOMAIN}.{DOMAIN}/admin/"
        save(accounts)
    else:
        step(f"tài khoản '{admin_user}' đã có từ lần chạy trước")
    return sys_token, tenant


def authorize_with_session(b, origin):
    """Đã có phiên đăng nhập trên host: authorize cấp code ngay (không qua trang đăng nhập) → token."""
    verifier = base64.urlsafe_b64encode(secrets.token_bytes(32)).rstrip(b"=").decode()
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
    redirect = origin + "/admin/callback"
    s, h, body = b.request("GET", "/connect/authorize?" + urllib.parse.urlencode({
        "client_id": "elib-admin", "response_type": "code", "redirect_uri": redirect, "scope": "openid profile offline_access elib-api",
        "code_challenge": challenge, "code_challenge_method": "S256", "state": "e2e"}))
    loc = h.get("Location", "")
    expect(s == 302 and loc.startswith(redirect), "authorize bằng phiên sẵn có", f"{s} {loc} {body}")
    code = urllib.parse.parse_qs(urllib.parse.urlsplit(loc).query)["code"][0]
    s, _, body = b.request("POST", "/connect/token", data={"grant_type": "authorization_code", "code": code, "redirect_uri": redirect,
                                                          "client_id": "elib-admin", "code_verifier": verifier})
    expect(s == 200, "đổi code lấy token", body)
    return json.loads(body)["access_token"]


def mailpit_latest(to, after_id=None, wait=20):
    """Thư mới nhất gửi tới địa chỉ `to` trong Mailpit (khác after_id) → (id, nội dung text)."""
    base = f"http://127.0.0.1:{ENV.get('MAILPIT_UI_PORT', '8025')}"
    for _ in range(max(1, wait * 2)):  # wait=0: chỉ đọc một lần (lấy mốc thư hiện có)
        with urllib.request.urlopen(f"{base}/api/v1/search?query=" + urllib.parse.quote(f"to:{to}"), timeout=10) as r:
            msgs = json.loads(r.read().decode())["messages"]
        if msgs and msgs[0]["ID"] != after_id:
            with urllib.request.urlopen(f"{base}/api/v1/message/{msgs[0]['ID']}", timeout=10) as r:
                msg = json.loads(r.read().decode())
            return msgs[0]["ID"], msg.get("Text") or re.sub("<[^>]+>", " ", msg.get("HTML", ""))
        time.sleep(0.5)
    return None, None


def tenant_steps(sysb, sys_token, tenant, accounts, admin_user):
    credentials = accounts  # `accounts` bị dùng lại làm danh sách tài khoản bạn đọc ở các bước sau
    host = f"{SUBDOMAIN}.{DOMAIN}"
    origin = f"https://{host}"
    print(f"4. Đăng nhập trên host đơn vị — {origin} (vòng qua gateway, chưa có DNS wildcard)")
    tb = Browser(GATEWAY, host=host, proto="https")
    password = accounts.get("TENANT_ADMIN_PASSWORD") or accounts["TENANT_ADMIN_TEMP"]
    token = pkce_login(tb, origin, admin_user, password)
    s, _, body = tb.request("GET", "/api/me", token=token)
    me = json.loads(body)
    expect(s == 200 and me["tenantId"] and (tenant is None or me["tenantId"] == tenant["id"]),
           f"/api/me: tenant_id={me['tenantId']}, quyền={me['permissions']}")
    if me["mustChangePassword"]:
        new = "Demo" + secrets.token_hex(6) + "5"
        s, _, body = tb.request("POST", "/api/me/password", token=token, json_body={"currentPassword": password, "newPassword": new})
        expect(s == 204, "đổi mật khẩu bắt buộc lần đầu", body)
        accounts["TENANT_ADMIN_PASSWORD"] = new
        accounts.pop("TENANT_ADMIN_TEMP", None)
        save(accounts)
        s, _, body = tb.request("GET", "/api/me", token=token)
        expect(not json.loads(body)["mustChangePassword"], "cờ buộc đổi mật khẩu đã tắt")
    s, _, body = tb.request("GET", "/api/admin/tenant/features", token=token)
    modules = set(json.loads(body)["modules"]) if s == 200 else set()
    required = {"CATALOG", "CIRCULATION", "HOLDINGS", "SEARCH"}  # ACQUISITION: có khi quản trị nền tảng đã bán (bước 2) — không bắt buộc ở --tenant-only
    expect(required <= modules <= required | {"ACQUISITION", "PAYMENT", "DIGITAL", "PORTAL", "SPACE", "SERIALS"}, f"menu theo license: {sorted(modules)}", body)
    s, _, body = tb.request("GET", "/api/admin/identity/users", token=token)
    expect(s == 200 and any(u["userName"] == admin_user for u in json.loads(body)["items"]), "API người dùng của đơn vị", body)

    print("5. Cô lập")
    s, _, body = sysb.request("GET", "/api/admin/identity/users", token=token)
    expect(s in (403, 404), f"token đơn vị dùng trên host hệ thống bị chặn ({s})", body)
    s, _, body = tb.request("GET", "/api/system/tenant/tenants", token=token)
    expect(s in (403, 404), f"nhân viên đơn vị không gọi được API quản trị nền tảng ({s})", body)

    print("6. Danh mục và tham số của đơn vị")
    if sys_token:
        s, _, body = sysb.request("POST", f"/api/system/tenant/tenants/{tenant['publicId']}/defaults", token=sys_token)
        expect(s == 200, f"bổ sung danh mục mặc định (thêm {json.loads(body).get('added') if s == 200 else '?'} mục)", body)
    s, _, body = tb.request("POST", "/api/admin/tenant/ethnicities/SearchAll", token=token, json_body={})
    expect(s == 200 and len(json.loads(body)) >= 55, f"danh mục dân tộc: {len(json.loads(body)) if s == 200 else '?'} mục", body)
    s, _, body = tb.request("POST", "/api/admin/tenant/system-parameters/Search", token=token, json_body={"keyword": "LIBRARYNAME"})
    param = json.loads(body)["items"][0] if s == 200 and json.loads(body)["items"] else None
    expect(param is not None, "tham số LIBRARYNAME có sẵn", body)
    s, _, body = tb.request("PUT", f"/api/admin/tenant/system-parameters/Update/{param['publicId']}", token=token,
                            json_body={"code": param["code"], "value": "Thư viện Trường demo", "description": param["description"], "type": "text"})
    expect(s == 200, "sửa tham số", body)
    opac = Browser(GATEWAY, host=host, proto="https")
    s, _, body = opac.request("GET", "/api/opac/tenant/parameters?codes=LibraryName,READER_AUTH_CONFIG")
    values = {p["code"]: p["value"] for p in json.loads(body)} if s == 200 else {}
    expect(values == {"LIBRARYNAME": "Thư viện Trường demo"}, f"OPAC ẩn danh chỉ thấy tham số công khai: {values}", body)

    print("7. Gửi email (notification)")
    s, _, body = tb.request("GET", "/api/admin/notification/email-settings", token=token)
    expect(s == 200 and json.loads(body)["platformAvailable"], f"cấu hình email: {body}", body)
    s, _, body = tb.request("POST", "/api/admin/notification/email-templates/RestoreDefaults", token=token)
    expect(s == 200, f"mẫu email mặc định (thêm {json.loads(body).get('added') if s == 200 else '?'} mẫu)", body)
    s, _, body = tb.request("POST", "/api/admin/notification/email-templates/SearchAll", token=token, json_body={})
    codes = sorted(t["code"] for t in json.loads(body)) if s == 200 else []
    expect({"LOGIN_OTP", "TEST_EMAIL"} <= set(codes), f"mẫu của đơn vị: {codes}", body)
    rcpt = f"e2e-{secrets.token_hex(4)}@example.com"
    s, _, body = tb.request("POST", "/api/admin/notification/email-settings/test", token=token, json_body={"to": rcpt})
    sent = json.loads(body) if s == 200 else {}
    expect(sent.get("status") == "Sent" and sent.get("viaPlatform"), f"gửi thư kiểm tra tới {rcpt}: {sent.get('status')} {sent.get('error') or ''}", body)
    mailpit = f"http://127.0.0.1:{ENV.get('MAILPIT_UI_PORT', '8025')}"
    found = None
    for _ in range(20):
        with urllib.request.urlopen(f"{mailpit}/api/v1/search?query=" + urllib.parse.quote(f"to:{rcpt}"), timeout=10) as r:
            msgs = json.loads(r.read().decode())["messages"]
        if msgs: found = msgs[0]; break
        time.sleep(0.5)
    expect(found is not None, f"Mailpit nhận thư: \"{found and found['Subject']}\"")
    s, _, body = tb.request("POST", "/api/admin/notification/notification-logs/Search", token=token, json_body={"keyword": rcpt})
    expect(s == 200 and json.loads(body)["totalCount"] == 1, "nhật ký gửi tin có thư vừa gửi", body)
    s, _, body = tb.request("PUT", "/api/admin/notification/email-settings", token=token, json_body={
        "host": "postgres", "port": 5432, "security": "None", "fromAddress": "a@b.vn", "clearPassword": False})
    expect(s == 400 and "SMTP_HOST_NOT_ALLOWED" in body, "chặn SMTP trỏ vào mạng nội bộ (postgres:5432)", body)

    print("8. Phân quyền theo module")
    s, _, body = tb.request("GET", "/api/admin/identity/permission-catalog", token=token)
    catalog = json.loads(body) if s == 200 else []
    expect(any(m["code"] == "ORGS" for m in catalog), f"danh mục quyền: {len(catalog)} module", body)
    s, _, body = tb.request("GET", "/api/admin/identity/roles", token=token)
    role = next((r for r in json.loads(body) if r["name"] == "E2E thủ thư"), None)
    if role is None:
        s, _, body = tb.request("POST", "/api/admin/identity/roles", token=token, json_body={"name": "E2E thủ thư", "description": None, "permissions": []})
        expect(s == 201, "tạo vai trò 'E2E thủ thư'", body)
        role = json.loads(body)
    s, _, body = tb.request("PUT", f"/api/admin/identity/roles/{role['publicId']}", token=token,
                            json_body={"name": role["name"], "description": None, "permissions": ["ORGS:view", "orgs:add", "NOTIFICATION_LOGS:view"]})
    expect(s == 200 and json.loads(body)["permissions"] == ["NOTIFICATION_LOGS:view", "ORGS:add", "ORGS:view"], "gán quyền theo module", body)
    s, _, body = tb.request("PUT", f"/api/admin/identity/roles/{role['publicId']}", token=token,
                            json_body={"name": role["name"], "description": None, "permissions": ["CIRCULATION:add"]})
    expect(s == 400 and "PERMISSION_UNKNOWN" in body, "mã quyền ngoài danh mục bị từ chối", body)

    print("9. OTP và CAPTCHA khi đăng nhập")
    def set_param(code, value):
        s, _, body = tb.request("POST", "/api/admin/tenant/system-parameters/Search", token=token, json_body={"keyword": code})
        p = next(x for x in json.loads(body)["items"] if x["code"] == code)
        s, _, body = tb.request("PUT", f"/api/admin/tenant/system-parameters/Update/{p['publicId']}", token=token,
                                json_body={"code": code, "value": value, "description": p["description"], "type": p["type"]})
        expect(s == 200, f"{code} = {value}", body)

    otp_user, otp_email = "e2eotp", "e2e-otp@example.com"
    s, _, body = tb.request("GET", "/api/admin/identity/users?search=" + otp_user, token=token)
    if not any(u["userName"] == otp_user for u in json.loads(body)["items"]):
        accounts["E2E_OTP_PASSWORD"] = "Otp" + secrets.token_hex(6) + "3"
        save(accounts)
        s, _, body = tb.request("POST", "/api/admin/identity/users", token=token, json_body={
            "userName": otp_user, "fullName": "Thử OTP", "email": otp_email, "phone": None, "password": accounts["E2E_OTP_PASSWORD"], "roleIds": [role["publicId"]]})
        expect(s == 201, f"tạo tài khoản '{otp_user}' có email", body)
    before, _ = mailpit_latest(otp_email, wait=0)
    set_param("ADMIN_LOGIN_OTP_ENABLED", "1")
    try:
        asked = []
        def read_otp():
            asked.append(1)
            _, text = mailpit_latest(otp_email, after_id=before)
            m = re.search(r"\b(\d{6})\b", text or "")
            expect(m is not None, f"Mailpit nhận mã OTP gửi tới {otp_email}", text or "")
            return m.group(1)
        otp_token = None
        for attempt in range(10):  # cache tham số của identity được xoá qua event SystemParameterChanged — đợi tối đa vài giây
            otp_token = pkce_login(Browser(GATEWAY, host=host, proto="https"), origin, otp_user, accounts["E2E_OTP_PASSWORD"], otp_code=read_otp)
            if asked: break
            time.sleep(1)
        expect(bool(asked), "bật tham số → đăng nhập phải qua bước OTP")
        s, _, body = Browser(GATEWAY, host=host, proto="https").request("GET", "/api/me", token=otp_token)
        expect(s == 200 and json.loads(body)["userName"] == otp_user, "đăng nhập bằng mật khẩu + OTP thành công", body)
    finally:
        set_param("ADMIN_LOGIN_OTP_ENABLED", "0")

    set_param("ADMIN_LOGIN_CAPTCHA_ENABLED", "1")
    try:
        page = ""
        for _ in range(20):
            _, _, page = Browser(GATEWAY, host=host, proto="https").request("GET", "/account/login")
            if "captchaId" in page: break
            time.sleep(0.5)
        expect("captchaId" in page and "data:image/png;base64," in page, "trang đăng nhập hiện CAPTCHA (ảnh PNG)")
        expect("<text" not in page, "CAPTCHA không lộ ký tự trong HTML")
    finally:
        set_param("ADMIN_LOGIN_CAPTCHA_ENABLED", "0")
    for _ in range(20):
        _, _, page = Browser(GATEWAY, host=host, proto="https").request("GET", "/account/login")
        if "captchaId" not in page: break
        time.sleep(0.5)
    expect("captchaId" not in page, "tắt tham số → CAPTCHA biến mất (cache xoá qua SystemParameterChanged)")

    if sys_token:
        print("10. Quản trị nền tảng đóng vai đơn vị (chỉ đọc)")
        s, _, body = sysb.request("POST", "/api/system/identity/impersonation", token=sys_token,
                                  json_body={"tenantId": tenant["id"], "reason": "Kiểm thử e2e GĐ0"})
        expect(s == 200, "tạo vé đóng vai", body)
        ticket = urllib.parse.urlsplit(json.loads(body)["url"])
        ib = Browser(GATEWAY, host=host, proto="https")
        s, h, body = ib.request("GET", f"{ticket.path}?{ticket.query}")
        expect(s == 302 and h.get("Location") == "/admin/", "đổi vé lấy phiên trên host đơn vị", f"{s} {body}")
        s, _, body = Browser(GATEWAY, host=host, proto="https").request("GET", f"{ticket.path}?{ticket.query}")
        expect(s == 400, "vé chỉ dùng được một lần")
        imp_token = authorize_with_session(ib, origin)
        s, _, body = ib.request("GET", "/api/me", token=imp_token)
        me = json.loads(body) if s == 200 else {}
        expect(me.get("readOnly") and me.get("tenantId") == tenant["id"], f"/api/me: {me.get('fullName')}, chỉ đọc", body)
        s, _, body = ib.request("POST", "/api/admin/tenant/ethnicities/SearchAll", token=imp_token, json_body={})
        expect(s == 200, "đọc được dữ liệu đơn vị", body)
        s, _, body = ib.request("POST", "/api/admin/tenant/ethnicities/Add", token=imp_token, json_body={"name": "Không được thêm"})
        expect(s == 403 and "IMPERSONATION_READONLY" in body, "ghi bị chặn (IMPERSONATION_READONLY)", body)

    print("11. Nhật ký (audit)")
    def find_log(path, tok, browser, action, needle):
        for _ in range(30):
            s, _, body = browser.request("POST", path, token=tok, json_body={"action": action, "pageSize": 50})
            items = json.loads(body)["items"] if s == 200 else []
            hit = next((i for i in items if needle in (i.get("summary") or "") or needle in (i.get("actorName") or "")), None)
            if hit: return hit
            time.sleep(1)
        expect(False, f"nhật ký có {action} '{needle}'", body)
    hit = find_log("/api/admin/audit/audit-logs/Search", token, tb, "LOGIN", "qtdemo")
    step(f"đăng nhập: {hit['actorName']} lúc {hit['occurredAt'][:19]} từ {hit['ipAddress']}")
    hit = find_log("/api/admin/audit/audit-logs/Search", token, tb, "ROLE_UPDATE", "E2E thủ thư")
    step(f"đổi vai trò: {hit['summary'][:80]}")
    hit = find_log("/api/admin/audit/audit-logs/Search", token, tb, "UPDATE", "ADMIN_LOGIN_OTP_ENABLED")
    step(f"sửa tham số (Crud tự ghi): {hit['summary']}")
    if sys_token:
        hit = find_log("/api/system/audit/audit-logs/Search", sys_token, sysb, "IMPERSONATION_STARTED", "Kiểm thử e2e GĐ0")
        step(f"nhật ký nền tảng: {hit['summary']}")
        hit = find_log("/api/system/audit/audit-logs/Search", sys_token, sysb, "TENANT_RESYNC", TENANT_CODE)
        step(f"nhật ký nền tảng: {hit['summary']} ({hit['tenantName']})")
    s, _, body = tb.request("POST", "/api/system/audit/audit-logs/Search", token=token, json_body={})
    expect(s in (403, 404), f"nhân viên đơn vị không đọc được nhật ký nền tảng ({s})", body)

    print("12. File (media + MinIO qua gateway /s3)")
    pdf = b"%PDF-1.7\n% e2e " + secrets.token_hex(16).encode() + b"\n%%EOF\n"
    s, _, body = tb.request("POST", "/api/admin/media/files/uploads", token=token,
                            json_body={"purpose": "attachment", "fileName": "e2e.pdf", "contentType": "application/pdf", "size": len(pdf)})
    expect(s == 200, "xin URL upload (ký sẵn, hết hạn sau 10 phút)", body)
    ticket = json.loads(body)
    # Trình duyệt PUT thẳng vào URL ký — KHÔNG kèm token; chữ ký phải khớp sau khi đi qua gateway (Host = minio:9000).
    s, _, body = tb.request("PUT", ticket["uploadUrl"], raw=(pdf, "application/pdf"))
    expect(s == 200, "PUT nội dung qua /s3 (chữ ký URL hợp lệ sau gateway)", body)
    s, _, body = tb.request("PUT", ticket["uploadUrl"].replace("X-Amz-Signature=", "X-Amz-Signature=0"), raw=(pdf, "application/pdf"))
    expect(s == 403, f"URL bị sửa chữ ký → MinIO từ chối ({s})", body)
    s, _, body = tb.request("POST", f"/api/admin/media/files/{ticket['fileId']}/complete", token=token)
    expect(s == 200 and json.loads(body)["contentType"] == "application/pdf", "media kiểm tra nội dung (magic bytes) → Ready", body)
    s, _, body = tb.request("GET", f"/api/admin/media/files/{ticket['fileId']}/download", token=token)
    expect(s == 200, "lấy URL tải (ký, hết hạn sau 5 phút)", body)
    s, h, content = Browser(GATEWAY, host=host, proto="https").request("GET", json.loads(body)["url"], binary=True)
    expect(s == 200 and content == pdf, "tải lại đúng nội dung, không cần đăng nhập (chỉ có URL ký)", f"{s}")
    expect(h.get("X-Content-Type-Options") == "nosniff" and "sandbox" in h.get("Content-Security-Policy", ""), "file trả kèm nosniff + CSP sandbox")
    s, _, body = Browser(GATEWAY, host=host, proto="https").request("GET", urllib.parse.urlsplit(json.loads(body)["url"]).path)
    expect(s == 403, f"bỏ chữ ký → không đọc được file riêng tư ({s})", body)
    html_bytes = b"<html><script>alert(1)</script></html>"
    s, _, body = tb.request("POST", "/api/admin/media/files/uploads", token=token,
                            json_body={"purpose": "attachment", "fileName": "x.png", "contentType": "image/png", "size": len(html_bytes)})
    bad = json.loads(body)
    tb.request("PUT", bad["uploadUrl"], raw=(html_bytes, "image/png"))
    s, _, body = tb.request("POST", f"/api/admin/media/files/{bad['fileId']}/complete", token=token)
    expect(s == 400 and "MEDIA_TYPE_INVALID" in body, "HTML giả ảnh PNG bị từ chối và xoá", body)
    if sys_token:
        png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYPj/HwADAgH/eL9GtQAAAABJRU5ErkJggg==")
        s, _, body = sysb.request("POST", "/api/system/media/files/uploads", token=sys_token, json_body={
            "tenantId": tenant["id"], "purpose": "tenant-logo", "fileName": "logo.png", "contentType": "image/png", "size": len(png)})
        expect(s == 200, "quản trị nền tảng xin URL upload logo hộ đơn vị", body)
        logo = json.loads(body)
        s, _, body = sysb.request("PUT", logo["uploadUrl"], raw=(png, "image/png"))
        expect(s == 200, "PUT logo qua host hệ thống", body)
        s, _, body = sysb.request("POST", f"/api/system/media/files/{tenant['id']}/{logo['fileId']}/complete", token=sys_token)
        url = json.loads(body).get("url") if s == 200 else None
        expect(bool(url), f"logo công khai: {url}", body)
        s, _, body = sysb.request("PUT", f"/api/system/tenant/tenants/{tenant['publicId']}/branding", token=sys_token,
                                  json_body={"logoText": "TH Demo", "logoUrl": url})
        expect(s == 200, "gắn logo cho đơn vị", body)
        s, h, content = opac.request("GET", url, binary=True)
        expect(s == 200 and content == png and h.get("Content-Type") == "image/png", "OPAC tải logo ẩn danh (Content-Type theo nội dung thật)", f"{s}")
        s, _, body = opac.request("GET", "/api/opac/tenant/manifest.json")
        expect(s == 200 and json.loads(body)["icons"][0]["src"] == url, "manifest.json của đơn vị có logo", body)

    print("13. Nhập danh mục từ Excel (building block Crud)")
    XLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    s, h, content = tb.request("GET", "/api/admin/tenant/ethnicities/ImportTemplate", token=token, binary=True)
    expect(s == 200 and content[:2] == b"PK" and XLSX in h.get("Content-Type", ""), f"tải file mẫu .xlsx ({len(content)} byte)")
    tag = secrets.token_hex(3)
    names = [f"E2E dân tộc {tag} A", f"E2E dân tộc {tag} B"]
    s, _, body = tb.request("POST", "/api/admin/tenant/ethnicities/Import", token=token,
                            raw=multipart("file", "dantoc.xlsx", xlsx([["Tên"], ["Kinh"], [names[0]], [names[0]]]), XLSX))
    errors = json.loads(body).get("errors", []) if s == 400 else []
    expect(s == 400 and [e["row"] for e in errors] == [2, 4], f"file có dòng trùng → không nhập, báo lỗi dòng {[e['row'] for e in errors]}", body)
    s, _, body = tb.request("POST", "/api/admin/tenant/ethnicities/Import?skipDuplicates=true", token=token,
                            raw=multipart("file", "dantoc.xlsx", xlsx([["Name"], ["Kinh"], [names[0]], [None], [names[1]]]), XLSX))
    result = json.loads(body) if s == 200 else {}
    expect(s == 200 and (result.get("imported"), result.get("skipped")) == (2, 1), f"nhập Excel: {result.get('detail')}", body)
    s, _, body = tb.request("POST", "/api/admin/tenant/ethnicities/Search", token=token, json_body={"keyword": f"E2E dân tộc {tag}"})
    found = json.loads(body)["items"] if s == 200 else []
    expect(sorted(i["name"] for i in found) == names, "dòng đã nhập có trong danh mục")
    for item in found:  # dọn để lần chạy sau không tích luỹ dữ liệu thử
        tb.request("DELETE", f"/api/admin/tenant/ethnicities/Delete/{item['publicId']}", token=token)
    find_log("/api/admin/audit/audit-logs/Search", token, tb, "IMPORT", "2 bản ghi")
    step("nhật ký có một dòng IMPORT cho cả lần nhập")

    print("14. OPAC ở gốc host đơn vị")
    s, _, page = opac.request("GET", "/")
    expect(s == 200 and "<opac-root>" in page and "/api/opac/tenant/manifest.json" in page, "trang chủ OPAC (index.html có manifest của đơn vị)", page[:200])
    s, _, page = opac.request("GET", "/tim-kiem?q=toan")
    expect(s == 200 and "<opac-root>" in page, "route SPA của OPAC trả index.html")
    s, _, body = opac.request("GET", "/api/opac/tenant/features")
    expect(s == 200 and json.loads(body)["status"] == "Active", "OPAC đọc features ẩn danh", body)
    s, h, _ = sysb.request("GET", "/")
    expect(s == 302 and h.get("Location") == "/admin/", "host hệ thống: / → /admin/", f"{s}")

    print("15. Bạn đọc (service patron)")
    P = "/api/admin/patron"
    def named(resource, name):
        s, _, body = tb.request("POST", f"{P}/{resource}/SearchAll", token=token, json_body={"keyword": name})
        found = next((x for x in json.loads(body) if x["name"] == name), None) if s == 200 else None
        if found: return found
        s, _, body = tb.request("POST", f"{P}/{resource}/Add", token=token, json_body={"name": name})
        expect(s == 201, f"thêm {resource} '{name}'", body)
        return json.loads(body)
    rtype, cls = named("reader-types", "Học sinh"), named("classes", "E2E-6A")
    tag = secrets.token_hex(3).upper()
    card = f"E2E{tag}"
    s, _, body = tb.request("POST", f"{P}/readers/Add", token=token, json_body={
        "cardNo": card.lower(), "lastName": "Nguyễn Văn", "firstName": "Thử", "sex": 1, "birthDate": "2014-05-02",
        "readerTypeId": rtype["id"], "classId": cls["id"], "issueDate": "2026-09-05", "expireDate": "2027-06-30"})
    reader = json.loads(body) if s == 201 else {}
    expect(s == 201 and reader.get("cardNo") == card and reader.get("fullName") == "Nguyễn Văn Thử", f"thêm bạn đọc {card}", body)
    s, _, body = tb.request("POST", f"{P}/readers/Add", token=token, json_body={"cardNo": card, "firstName": "Trùng"})
    expect(s == 409 and "READER_CARDNO_EXISTS" in body, "số thẻ trùng bị từ chối", body)
    s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={"keyword": "văn thử", "classId": cls["id"]})
    expect(s == 200 and any(r["cardNo"] == card for r in json.loads(body)["items"]), "tìm theo họ tên + lớp", body)
    s, _, body = tb.request("POST", f"{P}/readers/Lock/{reader['publicId']}", token=token, json_body={"reason": "E2E mất thẻ"})
    expect(s == 200 and json.loads(body)["status"] == 1, "khoá thẻ", body)
    s, _, body = tb.request("POST", f"{P}/readers/Unlock/{reader['publicId']}", token=token)
    expect(s == 200 and json.loads(body)["status"] == 2, "mở khoá thẻ", body)
    s, _, body = tb.request("PUT", f"{P}/readers/BulkUpdate", token=token, json_body={"publicIds": [reader["publicId"]], "expireDate": "2028-06-30"})
    expect(s == 200 and json.loads(body)["updatedCount"] == 1, "sửa hàng loạt hạn thẻ", body)
    s, _, body = tb.request("POST", f"{P}/readers/Import", token=token, raw=multipart("file", "bandoc.xlsx", xlsx([
        ["Số thẻ", "Họ và tên", "Giới tính", "Loại bạn đọc", "Lớp"],
        [f"{card}-2", "Trần Thị Mai", "Nữ", "học sinh", "E2E-6A"],
        [f"{card}-3", "Lê Bình", "Nam", None, None]]), XLSX))
    expect(s == 200 and json.loads(body)["imported"] == 2, f"nhập bạn đọc từ Excel: {json.loads(body).get('detail') if s in (200, 400) else s}", body)
    s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={"keyword": card, "pageSize": 10})
    mine = json.loads(body)["items"] if s == 200 else []
    mai = next((r for r in mine if r["cardNo"] == f"{card}-2"), {})
    expect(len(mine) == 3 and mai.get("lastName") == "Trần Thị" and mai.get("classId") == cls["id"], "dòng nhập có đủ họ tên tách đúng, lớp theo tên")
    find_log("/api/admin/audit/audit-logs/Search", token, tb, "CHANGE_STATUS", card)
    step("nhật ký có khoá/mở thẻ bạn đọc (patron → audit)")

    # Mật khẩu OPAC ở identity: bạn đọc vừa nhập có thể chưa tới identity qua event → identity hỏi patron.
    RA = "/api/admin/identity/reader-accounts"
    ids = [r["publicId"] for r in mine]
    s, _, body = tb.request("PUT", f"{RA}/BulkPassword", token=token, json_body={"readerIds": ids, "useCardNo": True})
    expect(s == 200 and json.loads(body)["updatedCount"] == 3, "đặt mật khẩu OPAC = số thẻ cho 3 bạn đọc (identity, kể cả bạn đọc vừa nhập)", body)
    s, _, body = tb.request("PUT", f"{RA}/{reader['publicId']}/password", token=token, json_body={"password": "12345", "useCardNo": False})
    expect(s == 400 and "READER_PASSWORD_INVALID" in body, "mật khẩu bạn đọc dưới 6 ký tự bị từ chối", body)
    s, _, body = tb.request("PUT", f"{RA}/{reader['publicId']}/password", token=token, json_body={"password": "Thuvien2026", "useCardNo": False})
    expect(s == 200 and "Thuvien2026" not in body and json.loads(body)["mustChangePassword"], "đặt mật khẩu riêng (bắt đổi ở lần đăng nhập đầu, không trả mật khẩu)", body)
    s, _, body = tb.request("POST", f"{RA}/Status", token=token, json_body={"readerIds": ids})
    st = {a["readerPublicId"]: a for a in json.loads(body)} if s == 200 else {}
    expect(len(st) == 3 and not st[reader["publicId"]]["cardNoPassword"] and all(a["hasPassword"] for a in st.values())
           and sum(a["cardNoPassword"] for a in st.values()) == 2, "tình trạng đăng nhập OPAC trên danh sách bạn đọc", body)
    find_log("/api/admin/audit/audit-logs/Search", token, tb, "READER_PASSWORD_BULK", "3 bạn đọc")
    step("nhật ký có đặt mật khẩu OPAC (identity → audit, không ghi mật khẩu)")
    rb = Browser(GATEWAY, host=host, proto="https")
    rtoken = reader_login(rb, origin, f"{card}-2".lower(), f"{card}-2", "Docsach2026")
    s, _, body = rb.request("GET", "/api/opac/account/me", token=rtoken)
    expect(s == 200 and json.loads(body)["cardNo"] == f"{card}-2" and not json.loads(body)["mustChangePassword"], "OPAC: bạn đọc xem thông tin thẻ", body)
    s, _, body = rb.request("POST", f"{P}/readers/Search", token=rtoken, json_body={"keyword": card})
    s2, _, _ = rb.request("GET", "/api/me", token=rtoken)
    expect(s == 403 and s2 == 403, "token bạn đọc không dùng được API quản trị (gateway chặn theo loại tài khoản)", f"{s} {s2} {body[:200]}")

    # Quên mật khẩu (luôn có CAPTCHA — e2e chỉ kiểm tra chặn CAPTCHA sai) và OTP email khi bạn đọc đăng nhập (READER_LOGIN_OTP_ENABLED).
    fb = Browser(GATEWAY, host=host, proto="https")
    s, _, page = fb.request("GET", "/account/reader-forgot")
    af, cid = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page), re.search(r'name="captchaId" value="([^"]+)"', page)
    expect(s == 200 and af and cid, "trang quên mật khẩu bạn đọc (số thẻ + CAPTCHA)", page[:200])
    s, _, page = fb.request("POST", "/account/reader-forgot", data={"__RequestVerificationToken": html.unescape(af.group(1)), "cardNo": f"{card}-2",
                                                                   "captchaId": cid.group(1), "captcha": "XXXXX"})
    expect(s == 400 and "Mã xác nhận" in page, "quên mật khẩu: CAPTCHA sai bị chặn, không gửi thư", f"{s} {page[:200]}")

    def tenant_param(code, value):
        s, _, body = tb.request("POST", "/api/admin/tenant/system-parameters/Search", token=token, json_body={"keyword": code})
        prm = next(x for x in json.loads(body)["items"] if x["code"] == code)
        s, _, body = tb.request("PUT", f"/api/admin/tenant/system-parameters/Update/{prm['publicId']}", token=token,
                                json_body={"code": code, "value": value, "description": prm["description"], "type": prm["type"]})
        expect(s == 200, f"{code} = {value}", body)

    ocard, omail = f"OTP{tag.upper()}", f"e2e-reader-otp-{tag}@example.com"
    s, _, body = tb.request("POST", f"{P}/readers/Add", token=token, json_body={"cardNo": ocard, "lastName": "Thử", "firstName": "Mã OTP", "email": omail})
    oreader = json.loads(body) if s == 201 else {}
    s, _, body = tb.request("PUT", f"{RA}/{oreader.get('publicId')}/password", token=token, json_body={"password": None, "useCardNo": True})
    expect(s == 200, f"bạn đọc {ocard} có email, mật khẩu = số thẻ", body)
    before, _ = mailpit_latest(omail, wait=0)
    tenant_param("READER_LOGIN_OTP_ENABLED", "1")
    try:
        loc = ""
        for _ in range(10):  # identity xoá cache tham số khi nhận SystemParameterChanged
            ob = Browser(GATEWAY, host=host, proto="https")
            s, _, page = ob.request("GET", "/account/reader-login?returnUrl=%2F")
            af = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
            s, h, body = ob.request("POST", "/account/reader-login", data={"__RequestVerificationToken": html.unescape(af.group(1)), "cardNo": ocard,
                                                                          "password": ocard, "returnUrl": "/"})
            loc = path_of(h.get("Location", ""))
            if loc == "/account/reader-otp": break
            time.sleep(1)
        expect(loc == "/account/reader-otp", "bạn đọc đăng nhập đúng mật khẩu → bước nhập mã OTP", f"{s} {loc}")
        _, text = mailpit_latest(omail, after_id=before)
        code = re.search(r"\b(\d{6})\b", text or "")
        expect(code is not None, f"Mailpit nhận mã OTP gửi tới bạn đọc {omail}", text or "")
        s, _, page = ob.request("GET", "/account/reader-otp")
        af = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
        s, h, body = ob.request("POST", "/account/reader-otp", data={"__RequestVerificationToken": html.unescape(af.group(1)), "code": code.group(1)})
        expect(s == 302 and path_of(h.get("Location", "")) == "/account/reader-password", "đúng mã OTP → bước đổi mật khẩu lần đầu", f"{s} {body[:200]}")
    finally:
        tenant_param("READER_LOGIN_OTP_ENABLED", "0")
    tb.request("DELETE", f"{P}/readers/Delete/{oreader.get('publicId')}", token=token)

    png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYPj/HwADAgH/eL9GtQAAAABJRU5ErkJggg==")
    s, _, body = tb.request("POST", "/api/admin/media/files/uploads", token=token,
                            json_body={"purpose": "reader-photo", "fileName": f"{card}.png", "contentType": "image/png", "size": len(png)})
    photo = json.loads(body) if s == 200 else {}
    tb.request("PUT", photo.get("uploadUrl", "/s3/none"), raw=(png, "image/png"))
    s, _, body = tb.request("POST", f"/api/admin/media/files/{photo.get('fileId')}/complete", token=token)
    expect(s == 200 and json.loads(body)["url"] is None, "upload ảnh thẻ (bucket riêng tư, không có URL công khai)", body)
    s, _, body = tb.request("PUT", f"{P}/readers/Photo/{reader['publicId']}", token=token, json_body={"fileId": photo["fileId"]})
    expect(s == 200 and json.loads(body)["photoId"] == photo["fileId"], "gắn ảnh thẻ cho bạn đọc", body)
    s, _, body = tb.request("GET", f"/api/admin/media/files/{photo['fileId']}/download", token=token)
    s, _, content = Browser(GATEWAY, host=host, proto="https").request("GET", json.loads(body)["url"], binary=True) if s == 200 else (s, {}, b"")
    expect(s == 200 and content == png, "xem ảnh thẻ qua URL ký có hạn", f"{s}")
    s, _, body = tb.request("POST", f"{P}/readers/Photos", token=token,
                            json_body={"items": [{"cardNo": f"{card}-2".lower(), "fileId": photo["fileId"]}, {"cardNo": "KHONG-CO-" + tag, "fileId": photo["fileId"]}]})
    res = json.loads(body) if s == 200 else {}
    expect(res.get("matched") == 1 and res.get("notFound") == ["KHONG-CO-" + tag], f"gán ảnh hàng loạt theo số thẻ: {res}", body)

    s, h, content = tb.request("POST", f"{P}/readers/Export", token=token, binary=True,
                               json_body={"search": {"keyword": card}, "fields": ["cardno", "lastname", "firstname", "class"]})
    strings = ""
    if s == 200 and content[:2] == b"PK":
        with zipfile.ZipFile(io.BytesIO(content)) as z:
            strings = z.read("xl/sharedStrings.xml").decode("utf-8")
    expect(all(x in strings for x in (f"{card}-2", "Trần Thị", "E2E-6A", "Số thẻ")) and "Ngày sinh" not in strings,
           f"xuất Excel theo bộ lọc, chỉ các cột đã chọn ({len(content)} byte)", f"{s} {h.get('Content-Type')}")
    for r in mine:  # dọn bạn đọc thử
        tb.request("DELETE", f"{P}/readers/Delete/{r['publicId']}", token=token)
    s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={"keyword": card})
    expect(s == 200 and json.loads(body)["totalCount"] == 0, "đã dọn bạn đọc thử")

    print("15b. Nhập Excel nâng cao kiểu ELIB-LRC (ghép cột, xem trước, chạy nền)")
    lrc_class, n_rows = f"E2E-LRC-{tag}", 2100  # > 2.000: sửa hàng loạt theo bộ lọc đi nhánh chạy nền
    rows = [["DANH SÁCH BẠN ĐỌC (xuất từ ELIB-LRC)"], [], ["STT", "Ma the", "Ho dem", "Ten", "Ngay sinh", "Gioi tinh", "Lop hoc"]]
    rows += [[str(i), f"L{tag}-{i:04}", "Nguyễn Văn", f"Thử {i}", f"2012{(i % 12) + 1:02}{(i % 28) + 1:02}", "2" if i % 2 else "1", lrc_class]
             for i in range(1, n_rows + 1)]
    rows += [[str(n_rows + 1), f"L{tag}-X1", "Lỗi", "Ngày", "31/02/2012", "1", lrc_class], [str(n_rows + 2), f"L{tag}-0001", "Trùng", "Dòng", None, None, None]]
    lrc = xlsx(rows)
    s, _, body = tb.request("POST", f"{P}/readers/Import/Columns", token=token, raw=multipart("file", "lrc.xlsx", lrc, XLSX))
    cols = json.loads(body) if s == 200 else {}
    sug = cols.get("suggested", {})
    expect(cols.get("headerRow") == 3 and cols.get("dataRows") == n_rows + 2 and sug.get("cardno") == 2 and sug.get("lastname") == 3 and "class" not in sug,
           f"đọc cột: tiêu đề ở dòng {cols.get('headerRow')}, {cols.get('dataRows')} dòng, gợi ý {len(sug)} cột", body)
    options = json.dumps({"headerRow": 3, "columns": {**sug, "class": 7}, "overwrite": False, "readerTypeId": rtype["id"], "autoCreateRefs": True,
                          "opacCardNoPassword": True})
    s, _, body = tb.request("POST", f"{P}/readers/Import/Jobs", token=token, raw=multipart_fields([("file", "lrc.xlsx", lrc, XLSX)], {"options": options}))
    job = json.loads(body) if s == 200 else {}
    expect(job.get("status") == "Ready" and job.get("toCreate") == n_rows and job.get("errorCount") == 2 and job.get("hasErrorFile"),
           f"xem trước: thêm {job.get('toCreate')}, lỗi {job.get('errorCount')} — chưa ghi gì", body)
    s, _, body = tb.request("POST", f"{P}/readers/Import/Jobs/{job.get('id')}/Start", token=token)
    expect(s == 200 and json.loads(body)["status"] == "Queued", "bấm chạy nền", body)
    started = time.time()
    while time.time() - started < 300:
        s, _, body = tb.request("GET", f"{P}/readers/Import/Jobs/{job['id']}", token=token)
        job = json.loads(body) if s == 200 else job
        if job.get("status") in ("Done", "Failed"): break
        time.sleep(2)
    expect(job.get("status") == "Done" and job.get("created") == n_rows and job.get("failed") == 2,
           f"chạy nền xong sau {time.time() - started:.0f} giây: thêm {job.get('created')}, lỗi {job.get('failed')}", json.dumps(job)[:300])
    s, h, content = tb.request("GET", f"{P}/readers/Import/Jobs/{job['id']}/Errors", token=token, binary=True)
    expect(s == 200 and content[:2] == b"PK", f"tải file \"Dòng cần sửa\" ({len(content)} byte)", f"{s}")
    lrc_filter = {"cardNoContains": f"L{tag}-"}
    s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={**lrc_filter, "pageSize": 1})
    expect(s == 200 and json.loads(body)["totalCount"] == n_rows, "bạn đọc nhập được tìm thấy (lớp tự tạo, loại bạn đọc cho cả file)", body)
    s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={**lrc_filter, "pageSize": 20})
    sample = [r["publicId"] for r in json.loads(body)["items"]] if s == 200 else []
    accounts = []
    for _ in range(20):  # identity nhận ReaderChanged qua event
        s, _, body = tb.request("POST", "/api/admin/identity/reader-accounts/Status", token=token, json_body={"readerIds": sample})
        accounts = json.loads(body) if s == 200 else []
        if accounts and all(a["cardNoPassword"] for a in accounts): break
        time.sleep(1)
    expect(len(accounts) == 20 and all(a["cardNoPassword"] and a["mustChangePassword"] for a in accounts),
           "bạn đọc mới nhập có mật khẩu OPAC = số thẻ (identity theo cờ trong event, bắt đổi lần đầu)", json.dumps(accounts)[:300])

    print("15c. Thao tác hàng loạt theo bộ lọc, nhóm, lịch sử thay đổi")
    s, _, body = tb.request("POST", f"{P}/readers/Batch/Preview", token=token, json_body=lrc_filter)
    preview = json.loads(body) if s == 200 else {}
    expect(preview.get("count") == n_rows, f"xem trước theo bộ lọc: {preview.get('count')} bạn đọc", body)
    s, _, body = tb.request("PUT", f"{P}/readers/BatchUpdate", token=token,
                            json_body={"selection": {"search": {"cardNoContains": "L"}, "token": preview.get("token")}, "expireDate": "2029-06-30"})
    expect(s == 400 and "READER_BATCH_TOKEN_MISMATCH" in body, "mã xem trước không dùng được cho bộ lọc khác", body)
    t0 = time.time()
    s, _, body = tb.request("PUT", f"{P}/readers/BatchUpdate", token=token,
                            json_body={"selection": {"search": lrc_filter, "token": preview.get("token")}, "expireDate": "2029-06-30"},
                            headers={"X-Change-Reason": urllib.parse.quote("Gia hạn thẻ E2E")})
    queued = json.loads(body) if s == 200 else {}
    expect(queued.get("count") == n_rows and queued.get("jobId"), f"sửa hạn thẻ {n_rows} bạn đọc theo bộ lọc → chạy nền", body)
    bjob = {}
    while time.time() - t0 < 300:
        s, _, body = tb.request("GET", f"{P}/readers/Batch/Jobs/{queued.get('jobId')}", token=token)
        bjob = json.loads(body) if s == 200 else bjob
        if bjob.get("status") in ("Done", "Failed"): break
        time.sleep(2)
    expect(bjob.get("status") == "Done" and bjob.get("updated") == n_rows and bjob.get("failed") == 0,
           f"sửa hàng loạt chạy nền xong sau {time.time() - t0:.0f} giây: {bjob.get('updated')} bạn đọc", json.dumps(bjob)[:300])
    s, _, body = tb.request("POST", f"{P}/reader-groups/Add", token=token, json_body={"name": f"E2E nhóm {tag}", "code": f"E2E{tag}"})
    group = json.loads(body) if s == 201 else {}
    expect(s == 201, "tạo nhóm bạn đọc", body)
    s, _, body = tb.request("PUT", f"{P}/readers/Groups", token=token,
                            json_body={"selection": {"search": lrc_filter, "token": preview.get("token")}, "groupId": group.get("id"), "add": True})
    expect(s == 200 and json.loads(body)["count"] == n_rows, "thêm cả kết quả lọc vào nhóm", body)
    s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={"groupId": group.get("id"), "pageSize": 1})
    expect(s == 200 and json.loads(body)["totalCount"] == n_rows, "lọc danh sách theo nhóm", body)
    s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={"cardNo": f"L{tag}-0001"})
    first = json.loads(body)["items"][0]
    upd = {k: first[k] for k in ("cardNo", "lastName", "firstName", "citizenId", "cardUid", "email", "address", "birthDate", "sex", "readerTypeId",
                                 "classId", "courseId", "orgId", "degreeId", "ethnicityId", "academicTitleId", "issueDate", "expireDate")}
    upd["phone"] = "0912000111"
    s, _, body = tb.request("PUT", f"{P}/readers/Update/{first['publicId']}", token=token, json_body=upd,
                            headers={"X-Change-Reason": urllib.parse.quote("Cập nhật điện thoại theo đơn E2E")})
    expect(s == 200, "sửa bạn đọc kèm lý do sửa", body)
    hist = []
    for _ in range(15):  # audit ghi qua event
        s, _, body = tb.request("GET", f"/api/admin/audit/audit-logs/entity/Reader/{first['publicId']}", token=token)
        hist = json.loads(body) if s == 200 else []
        if any(x.get("changes") and "phone" in x["changes"] for x in hist): break
        time.sleep(1)
    edit = next((x for x in hist if x.get("changes") and "phone" in x["changes"]), {})
    ch = json.loads(edit.get("changes") or "{}")
    expect(ch.get("phone") == [None, "0912000111"] and ch.get("_reason") == "Cập nhật điện thoại theo đơn E2E"
           and any(x.get("changes") and "expireDate" in x["changes"] for x in hist),
           f"lịch sử thay đổi: {len(hist)} dòng, có sửa hàng loạt hạn thẻ và điện thoại cũ → mới kèm lý do", json.dumps(hist)[:300])
    ids = []
    for page_no in range(1, n_rows // 500 + 2):
        s, _, body = tb.request("POST", f"{P}/readers/Search", token=token, json_body={**lrc_filter, "pageSize": 500, "pageIndex": page_no})
        ids += [r["publicId"] for r in json.loads(body)["items"]] if s == 200 else []
    removed = 0
    for i in range(0, len(ids), 2000):  # xoá nhiều: tối đa 2.000 mỗi lần
        s, _, body = tb.request("POST", f"{P}/readers/BulkDelete", token=token, json_body={"publicIds": ids[i:i + 2000]})
        removed += json.loads(body)["count"] if s == 200 else 0
    expect(removed == n_rows, f"xoá nhiều {len(ids)} bạn đọc đã chọn (dọn dữ liệu thử)", body)
    s, _, body = tb.request("DELETE", f"{P}/reader-groups/Delete/{group.get('publicId')}", token=token)
    s2, _, body2 = tb.request("POST", f"{P}/classes/SearchAll", token=token, json_body={"keyword": lrc_class})
    for c in (json.loads(body2) if s2 == 200 else []):
        if c["name"] == lrc_class: tb.request("DELETE", f"{P}/classes/Delete/{c['publicId']}", token=token)
    expect(s == 204, "dọn nhóm và lớp thử", body)
    s, _, body = tb.request("POST", "/api/admin/media/files/urls", token=token, json_body={"ids": [photo["fileId"], str(uuid.uuid4())]})
    urls = json.loads(body) if s == 200 else []
    expect(len(urls) == 1 and urls[0]["id"] == photo["fileId"] and urls[0]["url"], "link xem nhiều ảnh một lần (media files/urls, bỏ id lạ)", body)
    lb = Browser(GATEWAY, host=host, proto="https")
    s, _, page = lb.request("GET", "/account/login?ReturnUrl=%2F", headers={"Cookie": "elib_lang=en"})
    expect(s == 200 and 'lang="en"' in page and "Sign in" in page, "trang đăng nhập identity hiện tiếng Anh theo cookie elib_lang", f"{s} {page[:200]}")

    print("16. Biên mục (service catalog)")
    C = "/api/admin/catalog"
    types = []
    for _ in range(30):  # catalog vừa triển khai: chờ tự dựng bản sao đơn vị + seed mặc định
        s, _, body = tb.request("POST", f"{C}/bib-types/SearchAll", token=token, json_body={})
        types = json.loads(body) if s == 200 else []
        if any(t["code"] == "SACH" for t in types): break
        time.sleep(2)
    book = next((t for t in types if t["code"] == "SACH"), None)
    expect(book is not None and len(types) >= 7, f"loại biểu ghi mặc định ({len(types)} loại, có Sách)", f"{s} {body[:200]}")
    s, _, body = tb.request("GET", f"{C}/worksheets/GetByBibType/{book['id']}", token=token)
    sheet = (json.loads(body) or [None])[0] if s == 200 else None
    expect(sheet is not None and any(f["tag"] == "245" for f in sheet["fields"]), f"biểu mẫu mặc định của Sách: {sheet and sheet['name']}", body[:200])
    s, _, body = tb.request("GET", f"{C}/marc21/fields", token=token)
    expect(s == 200 and any(f["tag"] == "650" for f in json.loads(body)), "từ điển MARC21 (tên trường tiếng Việt)", body[:200])

    tag = secrets.token_hex(3)
    isbn = "978604" + str(int(tag, 16) % 10_000_000).zfill(7)
    values = {"020": {"a": isbn}, "100": {"a": "Tô Hoài"}, "245": {"a": f"Dế Mèn phiêu lưu ký {tag} /", "c": "Tô Hoài"},
              "260": {"a": "H. :", "b": "Kim Đồng,", "c": "2020"}, "650": {"a": "Văn học thiếu nhi"}}
    fields = []
    for f in sheet["fields"]:  # điền vào biểu mẫu như cán bộ biên mục
        subs = [{"code": sf["code"], "value": values.get(f["tag"], {}).get(sf["code"], sf["value"])} for sf in (f.get("subfields") or [])]
        fields.append({**f, "subfields": subs})
    s, _, body = tb.request("POST", f"{C}/bibs/Add", token=token, json_body={"bibTypeId": book["id"], "worksheetId": sheet["id"], "fields": fields})
    bib = json.loads(body) if s == 201 else {}
    tags = {f["tag"]: f for f in bib.get("fields", [])}
    expect(s == 201 and tags.get("001", {}).get("value") == str(bib.get("mfn")) and tags.get("003", {}).get("value") == TENANT_CODE
           and "005" in tags and tags.get("008", {}).get("value", "")[7:11] == "2020",
           f"biên mục biểu ghi MFN {bib.get('mfn')} (001/003/005/008 tự sinh)", body[:300])
    expect(bib.get("title") == f"Dế Mèn phiêu lưu ký {tag}" and bib.get("isbns") == [isbn] and bib.get("leader", "")[6:8] == "am",
           "tóm tắt: nhan đề bỏ dấu ISBD, ISBN chuẩn hoá, Leader theo loại", json.dumps({k: bib.get(k) for k in ("title", "isbns", "leader")}, ensure_ascii=False))
    s, _, body = tb.request("POST", f"{C}/bibs/Search", token=token, json_body={"keyword": f"de men phieu luu ky {tag}"})
    expect(s == 200 and [b["mfn"] for b in json.loads(body)["items"]] == [bib["mfn"]], "tìm không dấu", body[:200])
    s, _, body = tb.request("GET", f"{C}/bibs/CheckIsbn?isbn={isbn[:3]}-{isbn[3:]}", token=token)
    expect(s == 200 and [m["mfn"] for m in json.loads(body)] == [bib["mfn"]], "cảnh báo trùng ISBN (gõ có gạch nối)", body[:200])
    s, _, body = tb.request("PUT", f"{C}/bibs/ChangeStatus", token=token, json_body={"publicId": bib["publicId"], "status": 1})
    s2, _, body2 = tb.request("GET", f"{C}/bibs/GetByMfn/{bib['mfn']}", token=token)
    expect(s == 204 and s2 == 200 and json.loads(body2)["status"] == 1, "ẩn biểu ghi khỏi OPAC", f"{s} {body[:100]}")
    find_log("/api/admin/audit/audit-logs/Search", token, tb, "ADD", f"Dế Mèn phiêu lưu ký {tag}")
    step("nhật ký có biên mục biểu ghi (catalog → audit)")

    # Từ điển tự học + gợi ý, lịch sử biên mục theo trường MARC có lý do sửa, nhật ký biên mục, cấu hình ISBD theo loại.
    s, _, body = tb.request("GET", f"{C}/dictionary/Suggest?kind=author&q=" + urllib.parse.quote("to hoa"), token=token)
    expect(s == 200 and any(x["value"] == "Tô Hoài" for x in json.loads(body)), "từ điển tác giả tự học khi lưu biểu ghi, gợi ý không dấu", body[:200])
    s, _, body = tb.request("GET", f"{C}/dictionary/Suggest?kind=publisher&q=kim", token=token)
    expect(s == 200 and any(x["value"] == "Kim Đồng" for x in json.loads(body)), "từ điển NXB (bỏ dấu câu cuối \"Kim Đồng,\")", body[:200])
    upd = [{**f, "subfields": [{**sf, "value": "Văn học Việt Nam" if f["tag"] == "650" and sf["code"] == "a" else sf["value"]} for sf in (f.get("subfields") or [])]}
           if f.get("subfields") else f for f in bib["fields"] if f["tag"] not in ("001", "005")]
    s, _, body = tb.request("PUT", f"{C}/bibs/Update/{bib['publicId']}", token=token,
                            json_body={"bibTypeId": book["id"], "fields": upd, "status": 1},
                            headers={"X-Change-Reason": urllib.parse.quote("Sửa từ khoá theo khung đề mục")})
    expect(s == 200, "sửa biểu ghi kèm lý do sửa", body[:200])
    hist = []
    for _ in range(15):
        s, _, body = tb.request("GET", f"/api/admin/audit/audit-logs/entity/Bib/{bib['publicId']}", token=token)
        hist = json.loads(body) if s == 200 else []
        if any(x.get("changes") and "650" in x["changes"] for x in hist): break
        time.sleep(1)
    ch = next((json.loads(x["changes"]) for x in hist if x.get("changes") and "650" in x["changes"]), {})
    expect(ch.get("650", [None, ""])[1].endswith("Văn học Việt Nam") and ch.get("_reason") == "Sửa từ khoá theo khung đề mục" and "245" not in ch,
           "lịch sử biên mục: chỉ trường 650 đổi (cũ → mới) kèm lý do", json.dumps(ch, ensure_ascii=False)[:300])
    s, _, body = tb.request("POST", "/api/admin/audit/audit-logs/catalog/Search", token=token, json_body={"keyword": f"Dế Mèn phiêu lưu ký {tag}"})
    expect(s == 200 and json.loads(body)["totalCount"] >= 2, "nhật ký biên mục (thêm + sửa biểu ghi)", body[:200])
    s, _, body = tb.request("GET", f"{C}/isbd-configs/Template", token=token)
    expect(s == 200 and len(json.loads(body)) > 10, "mẫu quy tắc ISBD", body[:120])
    rules = [{"tag": "245"}, {"tag": "245", "code": "a"}, {"tag": "260", "prefix": " — "}, {"tag": "260", "code": "b"}]
    s, _, body = tb.request("POST", f"{C}/isbd-configs/Preview", token=token, json_body={"mfn": bib["mfn"], "rules": rules})
    expect(s == 200 and json.loads(body) == [f"Dế Mèn phiêu lưu ký {tag} / — Kim Đồng,"], "xem trước ISBD theo quy tắc đang soạn", body[:200])

    # Xuất/nhập file MARC: xuất biểu ghi vừa biên mục rồi nhập lại → bị bỏ qua vì trùng ISBN; MARCXML mới → nhập được.
    s, h, mrc = tb.request("POST", f"{C}/bibs/ExportMarc", token=token, binary=True,
                           json_body={"search": {"keyword": f"de men phieu luu ky {tag}"}, "format": "iso2709"})
    expect(s == 200 and mrc[24:].find(b"") > 0 and mrc.endswith(b"") and f"Dế Mèn phiêu lưu ký {tag}".encode() in mrc,
           f"xuất ISO2709 ({len(mrc)} byte, UTF-8)", str(mrc[:200]))
    s, _, body = tb.request("POST", f"{C}/bibs/ImportMarc", token=token, raw=multipart("file", "xuat.mrc", mrc, "application/marc"))
    r = json.loads(body) if s == 200 else {}
    expect(r.get("total") == 1 and r.get("imported") == 0 and r.get("skipped") == 1, "nhập lại file vừa xuất: bỏ qua biểu ghi trùng ISBN", body[:300])
    xml = f'''<?xml version="1.0" encoding="UTF-8"?>
<collection xmlns="http://www.loc.gov/MARC21/slim"><record><leader>00000nam a2200000 a 4500</leader>
<controlfield tag="001">CU-123</controlfield>
<datafield tag="100" ind1="1" ind2=" "><subfield code="a">Nguyễn Nhật Ánh</subfield></datafield>
<datafield tag="245" ind1="1" ind2="0"><subfield code="a">Mắt biếc {tag} /</subfield><subfield code="c">Nguyễn Nhật Ánh</subfield></datafield>
<datafield tag="260" ind1=" " ind2=" "><subfield code="b">Trẻ,</subfield><subfield code="c">2019</subfield></datafield>
</record><record><leader>00000nam a2200000 a 4500</leader><datafield tag="100" ind1="1" ind2=" "><subfield code="a">Thiếu nhan đề</subfield></datafield></record>
</collection>'''.encode()
    s, _, body = tb.request("POST", f"{C}/bibs/ImportMarc", token=token, raw=multipart("file", "cu.xml", xml, "application/xml"))
    expect(s == 400 and json.loads(body).get("rejected") and json.loads(body)["errors"][0]["record"] == 2,
           "MARCXML có biểu ghi lỗi → không nhập gì, báo lỗi theo số thứ tự biểu ghi", body[:300])
    s, _, body = tb.request("POST", f"{C}/bibs/ImportMarc?skipInvalid=true", token=token, raw=multipart("file", "cu.xml", xml, "application/xml"))
    r = json.loads(body) if s == 200 else {}
    s2, _, body2 = tb.request("POST", f"{C}/bibs/Search", token=token, json_body={"keyword": f"mat biec {tag}"})
    found = json.loads(body2)["items"] if s2 == 200 else []
    expect(r.get("imported") == 1 and r.get("failed") == 1 and len(found) == 1 and found[0]["author"] == "Nguyễn Nhật Ánh",
           f"nhập MARCXML bỏ qua biểu ghi lỗi (MFN mới {found and found[0]['mfn']})", body[:300])
    s, _, _ = tb.request("DELETE", f"{C}/bibs/Delete/{found[0]['publicId']}", token=token) if found else (0, None, None)
    expect(s == 204, "xoá biểu ghi nhập thử")

    s, _, _ = tb.request("DELETE", f"{C}/bibs/Delete/{bib['publicId']}", token=token)
    s2, _, _ = tb.request("GET", f"{C}/bibs/GetByMfn/{bib['mfn']}", token=token)
    expect(s == 204 and s2 == 404, "xoá biểu ghi thử")

    print("17. Kho (service holdings)")
    H = "/api/admin/holdings"
    stores = []
    for _ in range(30):  # holdings vừa triển khai: chờ tự dựng bản sao đơn vị + seed kho chung
        s, _, body = tb.request("POST", f"{H}/stores/SearchAll", token=token, json_body={})
        stores = json.loads(body) if s == 200 else []
        if any(x["code"] == "KC" for x in stores): break
        time.sleep(2)
    kc = next((x for x in stores if x["code"] == "KC"), None)
    s, _, body = tb.request("POST", f"{H}/store-types/SearchAll", token=token, json_body={})
    expect(kc is not None and s == 200 and len(json.loads(body)) >= 2, f"loại kho + kho chung mặc định ({len(stores)} kho)", f"{s} {body[:200]}")

    s, _, body = tb.request("POST", f"{C}/bibs/Add", token=token, json_body={"bibTypeId": book["id"], "fields": [
        {"tag": "100", "ind1": "1", "ind2": " ", "subfields": [{"code": "a", "value": "Nguyễn Nhật Ánh"}]},
        {"tag": "245", "ind1": "1", "ind2": "0", "subfields": [{"code": "a", "value": f"Kính vạn hoa {tag}"}]}]})
    kbib = json.loads(body) if s == 201 else {}
    expect(s == 201, f"biên mục biểu ghi để đăng ký cá biệt (MFN {kbib.get('mfn')})", body[:200])
    prefix = f"T{tag.upper()}-"
    s, _, body = tb.request("POST", f"{H}/items/Register", token=token,
                            json_body={"mfn": kbib["mfn"], "quantity": 3, "prefix": prefix.lower(), "digits": 4, "storeId": kc["id"]})
    items = json.loads(body) if s == 200 else []
    expect([i["barcode"] for i in items] == [f"{prefix}000{n}" for n in (1, 2, 3)]
           and all(i["status"] == "I" and i["title"] == f"Kính vạn hoa {tag}" and i["storeCode"] == "KC" for i in items),
           f"đăng ký 3 ĐKCB theo lô {prefix}0001–0003 (nhan đề từ catalog)", body[:300])
    s, _, body = tb.request("GET", f"{H}/items/NextBarcode?prefix={prefix}&digits=4", token=token)
    expect(s == 200 and json.loads(body)["barcode"] == f"{prefix}0004", "số ĐKCB tiếp theo", body[:200])
    s, _, body = tb.request("POST", f"{H}/items/Add", token=token, json_body={"mfn": kbib["mfn"], "barcode": f"{prefix.lower()}0002"})
    expect(s == 409, "ĐKCB trùng (không phân biệt hoa/thường) bị từ chối", f"{s} {body[:200]}")
    s, _, body = tb.request("POST", f"{H}/items/Shelve", token=token, json_body={"barcodes": [f"{prefix.lower()}0001", f"{prefix}0002", "KHONG-CO"]})
    r = json.loads(body) if s == 200 else {}
    expect(r.get("shelved") == 2 and r.get("notFound") == ["KHONG-CO"], "xếp giá 2 bản bằng mã quét", body[:200])
    s, _, body = tb.request("POST", f"{H}/items/Lookup", token=token, json_body={"keyword": prefix, "itemStatus": "R"})
    expect(s == 200 and json.loads(body)["totalCount"] == 2, "tìm tài liệu theo trạng thái sẵn sàng", body[:200])
    s, _, body = tb.request("GET", f"{H}/stores/{kc['id']}", token=token)
    expect(s == 200 and json.loads(body)["itemCount"] >= 3, f"kho chung có {json.loads(body).get('itemCount')} bản", body[:200])
    s, _, body = tb.request("DELETE", f"{H}/stores/Delete/{kc['publicId']}", token=token)
    expect(s == 409 and json.loads(body).get("code") == "STORE_NOT_EMPTY", "kho còn bản sách không xoá được", f"{s} {body[:200]}")
    find_log("/api/admin/audit/audit-logs/Search", token, tb, "ADD", f"cho MFN {kbib['mfn']}")
    step("nhật ký có đăng ký cá biệt (holdings → audit)")
    for i in items:
        tb.request("DELETE", f"{H}/items/Delete/{i['publicId']}", token=token)
    s, _, body = tb.request("POST", f"{H}/items/Lookup", token=token, json_body={"keyword": prefix})
    tb.request("DELETE", f"{C}/bibs/Delete/{kbib['publicId']}", token=token)
    expect(s == 200 and json.loads(body)["totalCount"] == 0, "dọn ĐKCB và biểu ghi thử")

    print("18. Lưu thông (service circulation)")
    L = "/api/admin/circulation"
    places = []
    for _ in range(30):  # circulation vừa triển khai: chờ tự dựng bản sao đơn vị + seed quầy mượn trả
        s, _, body = tb.request("POST", f"{L}/circ-places/SearchAll", token=token, json_body={})
        places = json.loads(body) if s == 200 else []
        if any(x["code"] == "QUAY" for x in places): break
        time.sleep(2)
    quay = next((x for x in places if x["code"] == "QUAY"), None)
    if quay and (quay.get("storeIds") or quay.get("readerTypeIds")):  # quầy mặc định đã bị giới hạn kho/loại bạn đọc (dữ liệu đơn vị dùng lâu) → dùng điểm riêng không giới hạn
        e2eq = next((x for x in places if x["code"] == "E2EQ"), None)
        if e2eq is None:
            s, _, body = tb.request("POST", f"{L}/circ-places/Add", token=token, json_body={"code": "E2EQ", "name": "Quầy kiểm thử e2e"})
            e2eq = json.loads(body) if s == 201 else None
        quay = e2eq or quay
    s, _, body = tb.request("POST", f"{L}/loan-policies/SearchAll", token=token, json_body={})
    expect(quay is not None and s == 200 and any(p["loanDays"] == 14 for p in json.loads(body)), "quầy mượn trả + chính sách mặc định 14 ngày", f"{s} {body[:200]}")

    s, _, body = tb.request("POST", f"{C}/bibs/Add", token=token, json_body={"bibTypeId": book["id"], "fields": [
        {"tag": "245", "ind1": "1", "ind2": "0", "subfields": [{"code": "a", "value": f"Hoàng tử bé {tag}"}]}]})
    lbib = json.loads(body) if s == 201 else {}
    lprefix = f"L{tag.upper()}-"
    s, _, body = tb.request("POST", f"{H}/items/Register", token=token, json_body={"mfn": lbib.get("mfn"), "quantity": 2, "prefix": lprefix, "digits": 3})
    litems = json.loads(body) if s == 200 else []
    tb.request("POST", f"{H}/items/Shelve", token=token, json_body={"barcodes": [f"{lprefix}001"]})
    lcard = f"LT{tag.upper()}"
    s, _, body = tb.request("POST", f"{P}/readers/Add", token=token, json_body={"cardNo": lcard, "lastName": "Lê", "firstName": "Mượn"})
    lreader = json.loads(body) if s == 201 else {}
    expect(len(litems) == 2 and lreader, f"chuẩn bị biểu ghi MFN {lbib.get('mfn')}, 2 ĐKCB (1 đã xếp giá), bạn đọc {lcard}", body[:200])

    s, _, body = tb.request("POST", f"{L}/loans/Reader", token=token, json_body={"cardNo": lcard.lower(), "circPlaceId": quay["id"]})
    panel = json.loads(body) if s == 200 else {}
    expect(panel.get("fullName") == "Lê Mượn" and panel.get("canBorrow") and panel.get("loanDays") == 14, "thông tin bạn đọc ở quầy (bản sao từ patron)", body[:300])
    lb1 = None
    for _ in range(15):  # bản sách vừa xếp giá: bản sao ở circulation cập nhật qua ItemChanged
        s, _, body = tb.request("POST", f"{L}/loans/Checkout", token=token,
                                json_body={"cardNo": lcard, "barcodes": [f"{lprefix}001".lower(), f"{lprefix}002"], "circPlaceId": quay["id"]})
        r = json.loads(body) if s == 200 else {}
        if r.get("succeeded") == 1: break
        time.sleep(1)
    lines = r.get("lines", [])
    expect(r.get("succeeded") == 1 and lines[0]["loan"]["title"] == f"Hoàng tử bé {tag}" and "chưa xếp giá" in lines[1]["message"],
           "mượn: bản đã xếp giá thành công, bản chưa xếp giá bị từ chối", body[:400])
    loan = lines[0]["loan"] if lines and lines[0]["loan"] else {}
    s, _, body = tb.request("POST", f"{L}/loans/Checkout", token=token, json_body={"cardNo": lcard, "barcodes": [f"{lprefix}001"], "circPlaceId": quay["id"]})
    expect(s == 200 and json.loads(body)["lines"][0]["message"] == "Bạn đọc đang mượn tài liệu này.", "không mượn trùng bản đang mượn", body[:200])
    s, _, body = tb.request("POST", f"{L}/loans/Renew", token=token, json_body={"loanId": loan.get("publicId"), "reason": "E2E gia hạn"})
    expect(s == 200 and json.loads(body)["renewCount"] == 1, "gia hạn (lý do bắt buộc)", body[:200])
    s, _, body = tb.request("POST", f"{L}/loans/Search", token=token, json_body={"cardNo": lcard, "state": "open"})
    expect(s == 200 and json.loads(body)["totalCount"] == 1, "lịch sử: 1 lượt đang mượn", body[:200])
    s, _, body = tb.request("POST", f"{L}/loans/Return", token=token, json_body={"barcode": f" {lprefix}001 ", "circPlaceId": quay["id"]})
    expect(s == 200 and json.loads(body)["loan"]["returnedAt"] and json.loads(body)["overdueDays"] == 0, "trả bằng mã quét", body[:200])
    s, _, body = tb.request("POST", f"{L}/loans/Search", token=token, json_body={"cardNo": lcard, "state": "returned"})
    expect(s == 200 and json.loads(body)["totalCount"] == 1, "lịch sử: lượt đã trả", body[:200])
    find_log("/api/admin/audit/audit-logs/Search", token, tb, "ADD", f"thẻ {lcard}")
    step("nhật ký có lượt mượn (circulation → audit)")

    print("19. Lưu thông: đặt mượn, phạt, sao chụp, báo cáo")
    tb.request("POST", f"{H}/items/Shelve", token=token, json_body={"barcodes": [f"{lprefix}002"]})
    hcard, hmail = f"DM{tag.upper()}", f"e2e-hold-{tag}@example.com"
    s, _, body = tb.request("POST", f"{P}/readers/Add", token=token, json_body={"cardNo": hcard, "lastName": "Đỗ", "firstName": "Đặt", "email": hmail})
    hreader = json.loads(body) if s == 201 else {}
    for _ in range(15):  # bản 002 vừa xếp giá — chờ ItemChanged tới circulation
        s, _, body = tb.request("POST", f"{L}/loans/Checkout", token=token,
                                json_body={"cardNo": lcard, "barcodes": [f"{lprefix}001", f"{lprefix}002"], "circPlaceId": quay["id"]})
        r = json.loads(body) if s == 200 else {}
        if r.get("succeeded") == 2: break
        time.sleep(1)
    expect(r.get("succeeded") == 2 and hreader, f"bạn đọc {lcard} mượn cả 2 bản", body[:300])
    lost_loan = next(l["loan"] for l in r["lines"] if l["barcode"].upper() == f"{lprefix}001")
    s, _, body = tb.request("POST", f"{L}/holds/Place", token=token, json_body={"cardNo": hcard, "mfn": lbib.get("mfn"), "note": "E2E"})
    hold = json.loads(body) if s == 201 else {}
    expect(hold.get("status") == 1 and hold.get("queuePosition") == 1, "đặt mượn khi hết bản → xếp hàng thứ 1", body[:300])
    before, _ = mailpit_latest(hmail, wait=0)
    s, _, body = tb.request("POST", f"{L}/loans/Return", token=token, json_body={"barcode": f"{lprefix}002", "circPlaceId": quay["id"]})
    held = (json.loads(body) if s == 200 else {}).get("holdFor") or {}
    expect(held.get("cardNo") == hcard and held.get("status") == 2, f"trả {lprefix}002 → giữ cho người đặt {hcard}", body[:300])
    _, text = mailpit_latest(hmail, after_id=before)
    expect(text and "đã được giữ" in text and f"{lprefix}002" in text, "email \"sách đặt mượn đã sẵn sàng\" tới bạn đọc (notification)", str(text)[:200])
    s, _, body = tb.request("POST", f"{L}/loans/Checkout", token=token, json_body={"cardNo": lcard, "barcodes": [f"{lprefix}002"], "circPlaceId": quay["id"]})
    expect(s == 200 and "đang giữ cho bạn đọc đặt mượn" in json.loads(body)["lines"][0]["message"], "người khác không mượn được bản đang giữ", body[:200])
    s, _, body = tb.request("POST", f"{L}/loans/Checkout", token=token, json_body={"cardNo": hcard, "barcodes": [f"{lprefix}002"], "circPlaceId": quay["id"]})
    s2, _, body2 = tb.request("POST", f"{L}/holds/Search", token=token, json_body={"cardNo": hcard})
    expect(s == 200 and json.loads(body)["succeeded"] == 1 and json.loads(body2)["items"][0]["status"] == 3, "người đặt mượn được → đặt mượn hoàn thành", body2[:200])

    s, _, body = tb.request("POST", f"{L}/fine-tickets/BuildForReader", token=token, json_body={"cardNo": lcard, "loanIds": [lost_loan["publicId"]]})
    ticket = json.loads(body) if s == 200 else {}
    line = (ticket.get("lines") or [{}])[0]
    expect(line.get("reasonCode") == "MATTL" and line.get("loanOpen"), f"lập phiếu phạt {ticket.get('ticket', {}).get('code')}: dòng \"Mất tài liệu\"", body[:300])
    s, _, body = tb.request("PUT", f"{L}/fine-tickets/Save/{ticket['ticket']['publicId']}", token=token, json_body={
        "status": 2, "discount": 0, "paid": 50000, "note": "E2E đền sách", "lines": [{"id": line["id"], "reasonCode": "MATTL", "amount": 50000}], "deletedLineIds": [],
        "reason": "E2E: bạn đọc làm mất sách, đền đủ tiền"})
    saved = json.loads(body) if s == 200 else {}
    expect(saved.get("ticket", {}).get("remaining") == 0 and not saved["lines"][0]["loanOpen"], "lưu phiếu: nộp đủ 50.000đ, lượt mượn đóng vì mất", body[:300])
    expect([p["amount"] for p in saved.get("payments", [])] == [50000], "sổ thanh toán ghi đúng một khoản thu 50.000đ", body[:300])
    status = None
    for _ in range(20):  # holdings nhận LoanChanged (ClosedItemStatus = L)
        s, _, body = tb.request("POST", f"{H}/items/Lookup", token=token, json_body={"keyword": f"{lprefix}001"})
        status = (json.loads(body)["items"] or [{}])[0].get("status") if s == 200 else None
        if status == "L": break
        time.sleep(1)
    expect(status == "L", f"holdings: {lprefix}001 chuyển trạng thái \"Mất\"", str(status))
    s, _, body = tb.request("POST", f"{H}/items/Lookup", token=token, json_body={"keyword": f"{lprefix}002", "itemStatus": "B"})
    expect(s == 200 and json.loads(body)["totalCount"] == 1, f"holdings: {lprefix}002 hiện \"Đang mượn\" (lọc mã B)", body[:200])

    s, _, body = tb.request("POST", f"{L}/photocopies/Add", token=token,
                            json_body={"cardNo": hcard, "barcode": f"{lprefix}002", "fromPage": 1, "toPage": 10, "copies": 2, "unitPrice": 500})
    photo = json.loads(body) if s == 201 else {}
    expect(photo.get("total") == 10000, "sao chụp: 10 trang × 2 bản × 500đ = 10.000đ (tính ở server)", body[:200])
    today = time.strftime("%Y-%m-%d", time.gmtime(time.time() + 7 * 3600))
    s, _, body = tb.request("POST", f"{L}/reports/Search", token=token, json_body={"reportType": 1, "from": today, "to": today})
    daily = json.loads(body) if s == 200 else {}
    expect(daily.get("totalRow") and int(daily["totalRow"][1]) >= 3, f"báo cáo hoạt động phục vụ hôm nay: {daily.get('totalRow')}", body[:300])
    s, _, body = tb.request("POST", f"{L}/reports/Search", token=token, json_body={"reportType": 11, "from": today, "to": today, "pageSize": 100})
    expect(s == 200 and any(r[1].upper() == f"{lprefix}001" for r in json.loads(body)["rows"]), "báo cáo tài liệu mất có bản vừa báo mất", body[:300])
    s, h, body = tb.request("POST", f"{L}/loans/Export", token=token, json_body={"cardNo": lcard}, binary=True)
    expect(s == 200 and body[:2] == b"PK", "xuất Excel lịch sử lưu thông", str(s))

    print("19b. Trạm tự phục vụ SIP2 (cổng TCP của circulation)")
    login, kpass = f"e2e-{tag}", "Kiosk#" + secrets.token_hex(4)
    s, _, body = tb.request("POST", f"{L}/sip2-stations/Add", token=token, json_body={
        "code": "E2E", "name": "Trạm E2E", "loginId": login, "password": kpass, "circPlaceId": quay["id"]})
    station = json.loads(body) if s == 201 else {}
    expect(station.get("hasPassword") and "pbkdf2" not in body, "khai báo trạm SIP2 (mật khẩu chỉ lưu dạng băm)", body[:200])
    kiosk = Sip2Kiosk(int(ENV.get("SIP2_PORT", "16001")))
    now = time.strftime("%Y%m%d    %H%M%S", time.gmtime(time.time() + 7 * 3600))
    r, ok = kiosk.send(f"9300CN{login}|COsai-mat-khau|")
    expect(ok and r.startswith("940"), "đăng nhập trạm sai mật khẩu → 940", r)
    r, ok = kiosk.send(f"9300CN{login}|CO{kpass}|")
    expect(ok and r.startswith("941"), "đăng nhập trạm → 941", r)
    r, ok = kiosk.send("9900802.00")
    expect(ok and r.startswith("98YYYY") and Sip2Kiosk.field(r, "AM") == "Trạm E2E", "99 → 98 online, tên trạm (UTF-8, checksum đúng)", r)
    r, ok = kiosk.send(f"63000{now}          AO|AA{hcard}|")
    expect(ok and Sip2Kiosk.field(r, "AE") == "Đỗ Đặt" and Sip2Kiosk.field(r, "AU") == f"{lprefix}002", "63 thông tin bạn đọc: họ tên, tài liệu đang mượn", r)
    r, ok = kiosk.send(f"17{now}AO|AB{lprefix}002|")
    expect(ok and r[2:4] == "04" and Sip2Kiosk.field(r, "AJ") == f"Hoàng tử bé {tag}", "17 thông tin tài liệu: đang mượn, nhan đề", r)
    r, ok = kiosk.send(f"09N{now}{now}AP|AO|AB{lprefix}002|")
    expect(ok and r.startswith("101") and Sip2Kiosk.field(r, "AA") == hcard, "09 trả tại trạm", r)
    r, ok = kiosk.send(f"11NN{now}{now}AO|AA{hcard}|AB{lprefix}002|")
    expect(ok and r.startswith("121") and Sip2Kiosk.field(r, "AH"), "11 mượn tại trạm (hạn trả theo chính sách)", r)
    r, ok = kiosk.send(f"09N{now}{now}AP|AO|AB{lprefix}002|")
    expect(ok and r.startswith("101"), "09 trả lại (dọn)", r)
    kiosk.close()
    s, _, body = tb.request("POST", f"{L}/sip2-stations/Logs", token=token, json_body={"stationId": station.get("id"), "pageSize": 50})
    logs = json.loads(body)["items"] if s == 200 else []
    expect(len(logs) >= 7 and any(l["command"] == "93" and "CO***" in l["request"] for l in logs) and kpass not in body,
           f"nhật ký SIP2: {len(logs)} giao dịch, mật khẩu trạm đã che", body[:300])
    find_log("/api/admin/audit/audit-logs/Search", token, tb, "ADD", f"thẻ {hcard}")
    s, _, _ = tb.request("DELETE", f"{L}/sip2-stations/Delete/{station.get('publicId')}", token=token)
    expect(s == 204, "dọn trạm SIP2 thử")

    tb.request("DELETE", f"{L}/photocopies/Delete/{photo.get('publicId')}", token=token)
    tb.request("DELETE", f"{P}/readers/Delete/{hreader['publicId']}", token=token)
    for i in litems:
        tb.request("DELETE", f"{H}/items/Delete/{i['publicId']}", token=token)
    tb.request("DELETE", f"{C}/bibs/Delete/{lbib['publicId']}", token=token)
    s, _, _ = tb.request("DELETE", f"{P}/readers/Delete/{lreader['publicId']}", token=token)
    expect(s == 204, "dọn bạn đọc, ĐKCB, biểu ghi thử (lượt mượn giữ làm lịch sử)")

    print("20. Tra cứu OPAC (service search)")
    S = "/api/admin/search"
    s, _, body = tb.request("POST", f"{C}/bibs/Add", token=token, json_body={"bibTypeId": book["id"], "fields": [
        {"tag": "100", "ind1": "0", "ind2": " ", "subfields": [{"code": "a", "value": "Nguyễn Du"}]},
        {"tag": "245", "ind1": "1", "ind2": "0", "subfields": [{"code": "a", "value": f"Truyện Kiều {tag} /"}]},
        {"tag": "520", "ind1": " ", "ind2": " ", "subfields": [{"code": "a", "value": "Kiệt tác thơ lục bát của văn học Việt Nam."}]}]})
    sbib = json.loads(body) if s == 201 else {}
    sprefix = f"S{tag.upper()}-"
    s, _, body = tb.request("POST", f"{H}/items/Register", token=token, json_body={"mfn": sbib.get("mfn"), "quantity": 2, "prefix": sprefix, "digits": 3})
    sitems = json.loads(body) if s == 200 else []
    tb.request("POST", f"{H}/items/Shelve", token=token, json_body={"barcodes": [f"{sprefix}001"]})
    expect(sbib and len(sitems) == 2, f"chuẩn bị biểu ghi MFN {sbib.get('mfn')} (Nguyễn Du) + 2 ĐKCB, 1 bản đã xếp giá", body[:200])

    hit = {}
    for _ in range(30):  # BibChanged/ItemChanged → search (bất đồng bộ)
        s, _, body = opac.request("POST", "/api/opac/search/bibs/Search", json_body={"q": f"truyen kieu {tag}"})
        items = json.loads(body).get("items", []) if s == 200 else []
        hit = items[0] if items else {}
        if hit.get("copies") == 2 and hit.get("available") == 1: break
        time.sleep(1)
    expect(hit.get("title") == f"Truyện Kiều {tag}" and hit.get("available") == 1, "OPAC ẩn danh: tìm không dấu, 2 bản / 1 bản sẵn sàng", body[:300])
    s, _, body = opac.request("POST", "/api/opac/search/bibs/Search", json_body={"author": "nguyen du", "q": tag})
    expect(s == 200 and json.loads(body)["total"] == 1 and json.loads(body)["facets"]["authors"][0]["key"] == "Nguyễn Du",
           "tìm nâng cao theo tác giả + facet tác giả", body[:300])
    s, _, body = opac.request("GET", f"/api/opac/search/bibs/{sbib['publicId']}")
    detail = json.loads(body) if s == 200 else {}
    statuses = sorted(c["statusName"] for c in detail.get("holdings", []))
    expect(statuses == ["Sẵn sàng", "Đang xử lý"] and "lục bát" in (detail.get("summary") or ""), f"chi tiết: tóm tắt + tình trạng từng bản {statuses}", body[:300])
    s, _, body = opac.request("GET", "/api/opac/search/suggest?q=" + urllib.parse.quote(f"truyen kieu {tag}"))
    expect(s == 200 and json.loads(body) == [f"Truyện Kiều {tag}"], "gợi ý nhan đề khi gõ", body[:200])

    tb.request("PUT", f"{C}/bibs/ChangeStatus", token=token, json_body={"publicId": sbib["publicId"], "status": 1})
    total = None
    for _ in range(20):
        s, _, body = opac.request("POST", "/api/opac/search/bibs/Search", json_body={"q": f"truyen kieu {tag}"})
        total = json.loads(body)["total"] if s == 200 else None
        if total == 0: break
        time.sleep(1)
    expect(total == 0, "ẩn biểu ghi ở biên mục → biến mất khỏi OPAC", str(total))

    s, _, body = tb.request("GET", f"{S}/index/Status", token=token)
    st = json.loads(body) if s == 200 else {}
    expect(st.get("indexedBibs", 0) >= 1, f"quản trị chỉ mục: {st.get('indexedBibs')} biểu ghi, {st.get('indexedItems')} bản sách", body[:300])
    s, _, body = tb.request("POST", f"{S}/index/Rebuild", token=token)
    for _ in range(60):
        s2, _, body2 = tb.request("GET", f"{S}/index/Status", token=token)
        st = json.loads(body2) if s2 == 200 else {}
        if st.get("status") in ("Done", "Failed"): break
        time.sleep(1)
    expect(s == 202 and st.get("status") == "Done", f"dựng lại chỉ mục: {st.get('bibs')} biểu ghi, {st.get('items')} bản sách, {st.get('loans')} lượt đang mượn",
           body2[:300])

    for i in sitems:
        tb.request("DELETE", f"{H}/items/Delete/{i['publicId']}", token=token)
    s, _, _ = tb.request("DELETE", f"{C}/bibs/Delete/{sbib['publicId']}", token=token)
    expect(s == 204, "dọn ĐKCB, biểu ghi thử")

    print("21. Bổ sung (service acquisition): đơn đặt, phiếu nhận, khoá số lượng ĐKCB, giao nhận, báo cáo")
    s, _, body = tb.request("GET", "/api/admin/tenant/features", token=token)
    if s == 200 and "ACQUISITION" in json.loads(body)["modules"]:
        acquisition_steps(tb, token, tag, book, kc, admin_user)
    else:
        print("  [BỎ QUA] đơn vị chưa có phân hệ Bổ sung — chạy một lần không kèm --tenant-only để quản trị nền tảng bán thêm")

    print("21b. Thanh toán QR (service payment): VietQR/Sepay + VNPAY, webhook đã ký, gửi lại, sai số tiền")
    s, _, body = tb.request("GET", "/api/admin/tenant/features", token=token)
    if s == 200 and "PAYMENT" in json.loads(body)["modules"]:
        pcard = f"PY{tag.upper()}"  # bạn đọc riêng (bạn đọc của bước 18 đã bị xoá)
        s, _, body = tb.request("POST", "/api/admin/patron/readers/Add", token=token, json_body={"cardNo": pcard, "lastName": "Thanh", "firstName": "Toán"})
        preader = json.loads(body) if s == 201 else {}
        expect(bool(preader), f"tạo bạn đọc {pcard} cho bước thanh toán", body[:200])
        payment_steps(tb, token, tag, pcard)
        tb.request("DELETE", f"/api/admin/patron/readers/Delete/{preader['publicId']}", token=token)
    else:
        print("  [BỎ QUA] đơn vị chưa có phân hệ Thanh toán — chạy một lần không kèm --tenant-only để quản trị nền tảng bán thêm")

    print("21c. Thư viện số + cổng thông tin: duyệt đánh giá, ebook → chỉ mục tra cứu, đăng tin (HTML làm sạch), menu, liên hệ")
    s, _, body = tb.request("GET", "/api/admin/tenant/features", token=token)
    mods = json.loads(body)["modules"] if s == 200 else []
    if "DIGITAL" in mods and "PORTAL" in mods:
        digital_portal_steps(tb, token, tag)
        print("21d. Quản lý tài liệu số như LRC: trường giàu, lọc nâng cao, chuyển bộ sưu tập hàng loạt, xuất/nhập Excel và Dublin Core, file, phân quyền bộ sưu tập")
        digital_docs_steps(tb, token, tag)
        print("21e. Tài liệu số: tra ISBN, AI, đồng bộ nội dung PDF → tìm trong nội dung (OPAC), xuất gói DSpace kèm file → nhập lại")
        digital_content_steps(tb, token, tag)
    else:
        print("  [BỎ QUA] đơn vị chưa có phân hệ Thư viện số / Cổng thông tin — chạy một lần không kèm --tenant-only để quản trị nền tảng bán thêm")

    print("21f. Bảng điều khiển: GET /dashboard của từng service phải 200 (Postgres + RLS thật, khác SQLite của test)")
    s, _, body = tb.request("GET", "/api/admin/tenant/features", token=token)
    mods = json.loads(body)["modules"] if s == 200 else []
    for module, service in [("CIRCULATION", "circulation"), ("HOLDINGS", "holdings"), ("CATALOG", "catalog"), ("ACQUISITION", "acquisition"),
                            ("DIGITAL", "digital"), ("PORTAL", "portal")]:
        if module not in mods:
            print(f"  [BỎ QUA] {service}: đơn vị chưa có phân hệ {module}")
            continue
        s, _, text = tb.request("GET", f"/api/admin/{service}/dashboard", token=token)
        expect(s == 200 and text.startswith("{"), f"dashboard {service}", f"{s} {text[:200]}")
    s, _, text = tb.request("GET", "/api/admin/patron/dashboard", token=token)
    expect(s == 200 and text.startswith("{"), "dashboard patron", f"{s} {text[:200]}")
    if "SPACE" in mods:
        s, _, text = tb.request("GET", "/api/admin/space/dashboard", token=token)
        expect(s == 200 and text.startswith("{"), "dashboard space", f"{s} {text[:200]}")
    s, _, text = tb.request("GET", "/api/system/health", token=token)
    expect(s == 403, "sức khoẻ nền tảng: cán bộ đơn vị bị từ chối", f"{s} {text[:200]}")

    if "SERIALS" in mods:
        print("21h. Báo – tạp chí (service serials): danh mục mặc định, đăng ký, sinh kỳ, nhận kỳ, khiếu nại, thống kê, báo cáo, dashboard")
        serials_steps(tb, token, tag, book)

    if sys_token and "DIGITAL" in mods:
        print("21g. Dữ liệu dùng chung: đơn vị SHARED tạo tài liệu số + từ điển → tự sao chỉ-đọc sang đơn vị; đơn vị chỉ ẩn/hiện")
        shared_data_steps(sysb, sys_token, tb, token, credentials, tag)

    print("22. OPAC mở rộng: MARC/ISBD, ảnh bìa, thống kê tra cứu, Z39.50, giới hạn lượt tra cứu")
    s, _, body = tb.request("POST", f"{C}/bibs/Add", token=token, json_body={"bibTypeId": book["id"], "fields": [
        {"tag": "020", "ind1": " ", "ind2": " ", "subfields": [{"code": "a", "value": "9780747532699"}]},
        {"tag": "100", "ind1": "1", "ind2": " ", "subfields": [{"code": "a", "value": "Rowling, J. K."}]},
        {"tag": "245", "ind1": "1", "ind2": "0", "subfields": [{"code": "a", "value": f"Harry Potter và hòn đá phù thủy {tag} /"},
                                                              {"code": "c", "value": "J. K. Rowling"}]},
        {"tag": "260", "ind1": " ", "ind2": " ", "subfields": [{"code": "a", "value": "H. :"}, {"code": "b", "value": "Trẻ,"}, {"code": "c", "value": "2020"}]},
        {"tag": "300", "ind1": " ", "ind2": " ", "subfields": [{"code": "a", "value": "366 tr. ;"}, {"code": "c", "value": "20 cm"}]}]})
    hbib = json.loads(body) if s == 201 else {}
    expect(hbib.get("mfn"), f"chuẩn bị biểu ghi MFN {hbib.get('mfn')} có ISBN", body[:200])

    s, _, body = opac.request("GET", f"/api/opac/catalog/bibs/{hbib['publicId']}/marc")
    marc = json.loads(body) if s == 200 else {}
    isbd = (marc.get("isbd") or [""])[0]
    expect(isbd == f"Harry Potter và hòn đá phù thủy {tag} / J. K. Rowling. — H. : Trẻ, 2020. — 366 tr. ; 20 cm."
           and marc["fields"][0]["tag"] == "001", "OPAC ẩn danh: MARC (kèm 001) + ISBD", body[:300])
    s, h, raw = opac.request("GET", f"/api/opac/catalog/bibs/{hbib['publicId']}/export", binary=True)
    expect(s == 200 and raw[:5].isdigit() and raw.endswith(b"\x1d") and f"mfn-{hbib['mfn']}.mrc" in h.get("Content-Disposition", ""),
           f"tải biểu ghi .mrc ({len(raw)} byte)", str(h)[:200])
    s, h, raw = opac.request("GET", f"/api/opac/catalog/bibs/{hbib['publicId']}/export?format=marcxml", binary=True)
    expect(s == 200 and b"<record" in raw, "tải biểu ghi MARCXML")

    s, _, body = tb.request("GET", f"{C}/bibs/LookupCover?isbn=9780747532699", token=token)
    found = json.loads(body).get("url") if s == 200 else None
    print(f"  [{'OK' if found else 'CẢNH BÁO'}]  tra ảnh bìa theo ISBN (Google Books/Open Library): {found or 'không thấy / không ra được Internet'}")
    png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYPj/HwADAgH/eL9GtQAAAABJRU5ErkJggg==")
    s, _, body = tb.request("POST", "/api/admin/media/files/uploads", token=token,
                            json_body={"purpose": "bib-cover", "fileName": "bia.png", "contentType": "image/png", "size": len(png)})
    ticket = json.loads(body) if s == 200 else {}
    tb.request("PUT", ticket.get("uploadUrl", "/x"), raw=(png, "image/png"))
    s, _, body = tb.request("POST", f"/api/admin/media/files/{ticket.get('fileId')}/complete", token=token)
    cover = json.loads(body).get("url") if s == 200 else None
    s, _, body = tb.request("PUT", f"{C}/bibs/Cover", token=token, json_body={"publicId": hbib["publicId"], "coverUrl": cover})
    expect(s == 200 and json.loads(body).get("coverUrl") == cover and cover.startswith("/s3/media-public/"), "upload ảnh bìa (media bib-cover) và gắn vào biểu ghi",
           body[:200])
    s, _, body = tb.request("PUT", f"{C}/bibs/Cover", token=token, json_body={"publicId": hbib["publicId"], "coverUrl": "http://evil.example/x.png"})
    expect(s == 400, "ảnh bìa http/ngoài đơn vị bị từ chối", body[:200])
    hit = {}
    for _ in range(30):
        s, _, body = opac.request("POST", "/api/opac/search/bibs/Search", json_body={"q": f"harry potter {tag}"})
        items = json.loads(body).get("items", []) if s == 200 else []
        hit = items[0] if items else {}
        if hit.get("coverUrl") == cover: break
        time.sleep(1)
    query_id = json.loads(body).get("queryId") if s == 200 else None
    s2, h2, img = opac.request("GET", cover, binary=True)
    expect(hit.get("coverUrl") == cover and s2 == 200 and img[:4] == b"\x89PNG", "OPAC: kết quả tra cứu có ảnh bìa, ảnh tải công khai qua /s3", body[:200])

    s, _, _ = opac.request("POST", "/api/opac/search/stats/click", json_body={"queryId": query_id, "bibPublicId": hbib["publicId"], "position": 1})
    opac.request("POST", "/api/opac/search/bibs/Search", json_body={"q": f"khong co tai lieu nay {tag}"})
    s, _, body = tb.request("GET", f"{S}/stats/Summary", token=token)
    st = json.loads(body) if s == 200 else {}
    zero = [q["text"] for q in st.get("topZeroResults", [])]
    expect(st.get("searches", 0) >= 2 and st.get("withClicks", 0) >= 1 and (f"khong co tai lieu nay {tag}" in zero or len(zero) >= 20),  # danh sách top bị cắt: đơn vị dùng lâu có nhiều truy vấn trùng hơn
           f"thống kê tra cứu: {st.get('searches')} lượt, {st.get('zeroResults')} không có kết quả, {st.get('withClicks')} lượt mở kết quả", body[:300])

    Z = f"{S}/z3950-servers"
    s, _, body = tb.request("POST", f"{Z}/Add", token=token, json_body={"name": f"Library of Congress {tag}", "host": "lx2.loc.gov", "port": 210,
                                                                        "databaseName": "LCDB", "recordSyntax": "USMARC", "showOnOpac": True})
    loc = json.loads(body) if s == 201 else {}
    s, _, body = tb.request("POST", f"{Z}/Add", token=token, json_body={"name": f"Nội bộ {tag}", "host": "postgres", "port": 5432, "databaseName": "x"})
    internal = json.loads(body) if s == 201 else {}
    expect(loc.get("publicId") and internal.get("publicId"), "khai báo máy chủ Z39.50 (LOC) và một máy chủ trỏ vào mạng nội bộ", body[:200])
    s, _, body = tb.request("POST", "/api/admin/search/z3950/Search", token=token, json_body={"title": "harry potter", "pageSize": 5})
    results = {r["serverName"]: r for r in (json.loads(body) if s == 200 else [])}
    blocked = results.get(f"Nội bộ {tag}", {})
    expect(s == 200 and not blocked.get("connected") and "mạng nội bộ" in (blocked.get("error") or ""), "máy chủ trong mạng nội bộ bị chặn", body[:300])
    lr = results.get(f"Library of Congress {tag}", {})
    if lr.get("connected") and lr.get("total", 0) > 0 and lr.get("hits"):
        first = lr["hits"][0]
        step(f"Z39.50 ra Library of Congress: {lr['total']} kết quả, bản ghi 1: {first.get('title')} — {len(first['record']['fields'])} trường MARC")
    else:
        print(f"  [CẢNH BÁO] Z39.50 ra Library of Congress không có kết quả: {lr.get('error') or body[:200]}")
    s, _, body = opac.request("GET", "/api/opac/search/z3950/servers")
    names = [x["name"] for x in json.loads(body)] if s == 200 else []
    expect(f"Library of Congress {tag}" in names and f"Nội bộ {tag}" not in names, "OPAC chỉ thấy máy chủ bật \"hiện trên OPAC\"", body[:200])
    for server in (loc, internal):
        tb.request("DELETE", f"{Z}/Delete/{server['publicId']}", token=token)
    s, _, _ = tb.request("DELETE", f"{C}/bibs/Delete/{hbib['publicId']}", token=token)
    expect(s == 204, "dọn máy chủ Z39.50, biểu ghi thử")

    # Giới hạn lượt tra cứu: chạy cuối — IP này bị chặn OPAC tới khi cửa sổ trượt qua (~10 giây mỗi đoạn, tối đa 1 phút).
    limited = None
    for i in range(400):
        s, h, _ = opac.request("GET", "/api/opac/search/suggest?q=ha")
        if s == 429:
            limited = (i + 1, h.get("Retry-After"))
            break
    expect(limited and limited[1], f"giới hạn lượt tra cứu theo IP: request thứ {limited and limited[0]} bị 429, Retry-After {limited and limited[1]} giây")


def acquisition_steps(tb, token, tag, book, kc, admin_user):
    """21. Bổ sung (acquisition) — chạy sau catalog/holdings và trước OPAC mở rộng (bước 22 chặn OPAC của IP này).
    Dữ liệu thử (biểu ghi, đơn đặt, phiếu nhận đã hoàn tất, ĐKCB đã ký nhận vào kho) giữ lại vì phiếu hoàn tất và bản đã giao nhận bị khoá;
    mỗi lần chạy dùng nhan đề / tiền tố theo `tag` nên không đụng nhau."""
    A, C, H = "/api/admin/acquisition", "/api/admin/catalog", "/api/admin/holdings"
    today = time.strftime("%Y-%m-%d", time.gmtime(time.time() + 7 * 3600))

    def post(path, body):
        s, _, text = tb.request("POST", path, token=token, json_body=body)
        return s, text, (json.loads(text) if text.startswith(("{", "[")) else {})

    sources = []
    for _ in range(30):  # acquisition vừa triển khai: chờ tự dựng bản sao đơn vị + seed nguồn bổ sung
        s, text, sources = post(f"{A}/sources/SearchAll", {})
        sources = sources if s == 200 else []
        if len(sources) >= 4: break
        time.sleep(2)
    expect(len(sources) >= 4, f"nguồn bổ sung mặc định: {', '.join(x['name'] for x in sources)}", str(sources)[:200])

    bibs = []
    for title in (f"Bổ sung A {tag}", f"Bổ sung B {tag}"):
        s, text, bib = post(f"{C}/bibs/Add", {"bibTypeId": book["id"], "fields": [
            {"tag": "100", "ind1": "1", "ind2": " ", "subfields": [{"code": "a", "value": "Tô Hoài"}]},
            {"tag": "245", "ind1": "1", "ind2": "0", "subfields": [{"code": "a", "value": title}]}]})
        expect(s == 201, f"biên mục '{title}' (MFN {bib.get('mfn')})", text[:200])
        bibs.append(bib)
    s, text, found = post(f"{A}/bibs/Lookup", {"keyword": str(bibs[0]["mfn"])})
    expect(s == 200 and any(b["mfn"] == bibs[0]["mfn"] for b in found), "tìm biểu ghi theo MFN ở acquisition (bản sao, thiếu thì hỏi catalog)", text[:200])
    s, _, text = tb.request("GET", "/api/admin/tenant/currencies/Options", token=token)
    usd = next((c for c in json.loads(text) if c["code"] == "USD"), None) if s == 200 else None
    expect(usd is not None, f"tiền tệ đang dùng cho đơn đặt / phiếu nhận (USD tỷ giá gợi ý {usd and usd['exchangeRate']})", f"{s} {text[:200]}")

    s, text, supplier = post(f"{A}/suppliers/Add", {"name": f"NCC {tag}", "email": "ncc@example.vn"})
    expect(s == 201, f"nhà cung cấp {supplier.get('name')}", text[:200])
    s, text, order = post(f"{A}/orders/Add", {"name": f"Đặt sách {tag}", "orderedOn": today, "supplierId": supplier["id"], "paymentMethod": 1, "status": 2})
    expect(s == 201 and order["paymentMethod"] == 1, f"đơn đặt #{order.get('id')} (chuyển khoản, đã gửi đặt)", text[:200])
    s, text, oline = post(f"{A}/orders/SaveLine/{order['publicId']}", {"mfn": bibs[0]["mfn"], "quantity": 5, "price": 12.5, "currency": "usd", "rate": 25000})
    expect(s == 200 and oline["currency"] == "USD" and oline["rate"] == 25000 and oline["remaining"] == 5, "dòng đặt 5 bản × 12,5 USD, tỷ giá 25.000", text[:200])
    s, text, _ = post(f"{A}/orders/SaveLine/{order['publicId']}", {"mfn": bibs[1]["mfn"], "quantity": 2, "rate": 0})
    expect(s == 400 and "RATE_INVALID" in text, "tỷ giá không dương bị từ chối", f"{s} {text[:200]}")
    s, _, text = tb.request("GET", f"{A}/orders/GetById/{order['publicId']}", token=token)
    expect(s == 200 and json.loads(text)["total"] == 5 * 12.5 * 25000, f"tổng giá trị đơn = {json.loads(text).get('total')}", text[:200])

    s, text, receipt = post(f"{A}/receipts/Add", {"receivedOn": today, "name": f"Nhận {tag}", "supplierId": supplier["id"], "storeId": kc["id"],
                                                  "paymentStatus": 2, "paymentMethod": 1})
    expect(s == 201 and receipt["paymentStatus"] == 2, f"phiếu nhận #{receipt.get('id')} (đã thanh toán, chuyển khoản)", text[:200])
    s, text, rline = post(f"{A}/receipts/SaveLine/{receipt['publicId']}", {"orderLinePublicId": oline["publicId"], "quantity": 3})
    expect(s == 200 and rline["mfn"] == bibs[0]["mfn"] and rline["rate"] == 25000 and rline["currency"] == "USD" and rline["price"] == 12.5,
           "nhận 3 bản từ đơn đặt: biểu ghi, giá, tiền tệ, tỷ giá theo dòng đặt", text[:300])
    s, text, over = post(f"{A}/receipts/SaveLine/{receipt['publicId']}", {"mfn": bibs[1]["mfn"], "orderLinePublicId": oline["publicId"], "quantity": 3})
    expect(s == 400 and "ORDER_LINE_EXCEEDED" in text, "nhận vượt số còn chờ của dòng đặt bị từ chối", f"{s} {text[:200]}")
    s, _, text = tb.request("GET", f"{A}/orders/Lines/{order['publicId']}", token=token)
    expect(s == 200 and json.loads(text)[0]["received"] == 3 and json.loads(text)[0]["remaining"] == 2, "đơn đặt: đã nhận 3, còn chờ 2", text[:200])

    prefix = f"BS{tag.upper()}-"
    s, text, items = post(f"{A}/receipts/RegisterItems/{rline['publicId']}", {"quantity": 3, "prefix": prefix, "digits": 4})
    expect(s == 200 and [i["barcode"] for i in items] == [f"{prefix}000{n}" for n in (1, 2, 3)], f"đăng ký 3 ĐKCB {prefix}0001–0003 theo dòng phiếu nhận", text[:300])
    s, text, _ = post(f"{A}/receipts/RegisterItems/{rline['publicId']}", {"quantity": 1, "prefix": prefix, "digits": 4})
    expect(s >= 400 and "RECEIPT_LINE_FULL" in text, "khoá số lượng: đăng ký thêm quá số bản nhận bị từ chối", f"{s} {text[:200]}")
    s, text, _ = post(f"{A}/receipts/RegisterItems/{rline['publicId']}", {"barcode": f"{prefix}9999"})
    expect(s >= 400 and "RECEIPT_LINE_FULL" in text, "khoá số lượng: ĐKCB riêng lẻ cũng bị từ chối khi dòng đã đủ", f"{s} {text[:200]}")
    s, text, _ = post(f"{A}/receipts/SaveLine/{receipt['publicId']}", {"linePublicId": rline["publicId"], "quantity": 1, "price": 12.5, "currency": "USD", "rate": 25000})
    expect(s >= 400 and "QUANTITY_BELOW_REGISTERED" in text, "không giảm số bản dưới số ĐKCB đã đăng ký", f"{s} {text[:200]}")
    s, text, page = post(f"{H}/items/Search", {"receiptPublicId": receipt["publicId"]})
    expect(s == 200 and page["totalCount"] == 3 and all(i["receiptCode"] == receipt["id"] for i in page["items"]), "holdings: 3 bản gắn với phiếu nhận (lọc theo đơn nhận)", text[:300])
    for _ in range(30):  # nhật ký của holdings ghi đúng cán bộ thao tác, không phải tài khoản service của acquisition
        s, text, logs = post("/api/admin/audit/audit-logs/Search", {"action": "ADD", "pageSize": 50})
        hit = next((i for i in logs.get("items", []) if f"theo phiếu nhận số {receipt['id']}" in (i.get("summary") or "")), None) if s == 200 else None
        if hit: break
        time.sleep(1)
    expect(hit is not None and admin_user in (hit.get("actorName") or ""), f"nhật ký holdings: đăng ký ĐKCB theo phiếu ghi người thao tác '{hit and hit.get('actorName')}'", text[:300])

    s, text, done = post(f"{A}/receipts/Complete/{receipt['publicId']}", None)
    expect(s == 200 and done["completedAt"], "hoàn tất (khoá) phiếu nhận", text[:200])
    s, text, _ = post(f"{A}/receipts/RegisterItems/{rline['publicId']}", {"barcode": f"{prefix}9998"})
    expect(s >= 400 and "RECEIPT_COMPLETED" in text, "phiếu đã hoàn tất: không đăng ký thêm ĐKCB", f"{s} {text[:200]}")
    options = []
    for _ in range(30):  # ReceiptChanged tới holdings (outbox → RabbitMQ) → đơn nhận hiện ở phiếu giao nhận
        s, _, text = tb.request("GET", f"{H}/deliveries/Receipts", token=token)
        options = json.loads(text) if s == 200 else []
        if any(o["code"] == receipt["id"] for o in options): break
        time.sleep(1)
    expect(any(o["code"] == receipt["id"] and o["pending"] == 3 for o in options), f"phiếu giao nhận chọn được đơn nhận #{receipt['id']} (còn 3 bản chưa phân bổ)", str(options)[:300])

    s, text, delivery = post(f"{H}/deliveries/Add", {"delivererName": f"NCC {tag}", "receiverName": "Thủ kho", "storeId": kc["id"], "receiptPublicId": receipt["publicId"]})
    expect(s == 201 and delivery["receiptCode"] == receipt["id"], f"phiếu giao nhận #{delivery.get('id')} theo đơn nhận", text[:200])
    s, text, cands = post(f"{H}/deliveries/Candidates", {"transferId": delivery["publicId"], "receiptPublicId": receipt["publicId"], "pageSize": 20})
    expect(s == 200 and cands["totalCount"] == 3, "màn chọn sách liệt kê đúng 3 bản của đơn nhận", text[:200])
    s, text, added = post(f"{H}/deliveries/AddLines/{delivery['publicId']}", {"itemIds": [i["publicId"] for i in cands["items"]]})
    expect(s == 200, "thêm 3 bản vào phiếu giao nhận", text[:200])
    s, _, text = tb.request("PUT", f"{H}/deliveries/Update/{delivery['publicId']}", token=token, json_body={
        "delivererName": f"NCC {tag}", "receiverName": "Thủ kho", "storeId": kc["id"], "receiptPublicId": receipt["publicId"], "status": 2})
    expect(s == 200, "phiếu giao nhận chuyển sang 'Đã nhận đủ'", text[:200])
    s, text, signed = post(f"{H}/deliveries/Sign/{delivery['publicId']}", None)
    expect(s == 200 and signed.get("done") == 3, f"ký nhận: xếp giá {signed.get('done')} bản vào kho nhận", text[:200])

    rp = {"from": today, "to": today, "receiptCodeFrom": receipt["id"], "receiptCodeTo": receipt["id"], "pageSize": 100}
    s, text, listed = post(f"{A}/reports/AcquisitionList", rp)
    expect(s == 200 and listed["total"] == 1 and listed["quantity"] == 3 and listed["amount"] == 3 * 12.5 * 25000, f"báo cáo danh sách bổ sung: {listed.get('quantity')} bản, {listed.get('amount')}", text[:300])
    s, text, lookup = post(f"{A}/reports/LookupLines", rp)
    expect(s == 200 and lookup["totalCount"] == 1 and f"Bổ sung A {tag}" in lookup["items"][0]["isbd"] and lookup["items"][0]["storeName"] == kc["name"],
           f"tra cứu đơn nhận theo dòng: {lookup['items'][0]['isbd'] if lookup.get('items') else ''}", text[:300])
    s, _, content = tb.request("POST", f"{A}/reports/LookupLinesExport", token=token, json_body=rp, binary=True)
    strings = ""
    if s == 200 and content[:2] == b"PK":
        with zipfile.ZipFile(io.BytesIO(content)) as z:
            strings = z.read("xl/sharedStrings.xml").decode("utf-8")
    expect(f"Bổ sung A {tag}" in strings and "TRA CỨU ĐƠN NHẬN" in strings, f"xuất Excel tra cứu đơn nhận ({len(content)} byte)", str(s))
    s, text, alloc = post(f"{A}/reports/StoreAllocation", rp)
    expect(s == 200 and alloc["total"] == 3 and alloc["items"][0]["storeName"] == kc["name"], f"phân bổ kho: {alloc.get('total')} bản vào {kc['name']}", text[:300])
    s, text, register = post(f"{A}/reports/AccessionRegister", rp)
    rows = register.get("items", [])
    expect(s == 200 and [r["barcode"] for r in rows] == [f"{prefix}000{n}" for n in (1, 2, 3)] and all(r["date"] == today and r["receiptId"] == receipt["id"] and r["price"] == 12.5 for r in rows),
           "sổ ĐKCB: 3 bản theo số đăng ký, ngày nhận + đơn nhận + đơn giá theo phiếu", text[:300])
    s, _, content = tb.request("POST", f"{A}/reports/AccessionRegisterExport", token=token, json_body=rp, binary=True)
    expect(s == 200 and content[:2] == b"PK", "xuất Excel sổ ĐKCB", str(s))
    s, text, newbooks = post(f"{A}/reports/NewBooks", {"from": today, "to": today, "pageSize": 100})
    expect(s == 200 and any(b["mfn"] == bibs[0]["mfn"] and b["catalogedOn"] == today for b in newbooks.get("items", [])), "thư mục sách mới theo ngày biên mục (catalog gửi kèm trong BibChanged)", text[:300])
    s, text, store_report = post(f"{H}/store-book-report/Search", {"storeId": kc["id"], "from": today, "to": today, "pageIndex": 1, "pageSize": 100})
    expect(s == 200 and any(r["receiptCode"] == receipt["id"] and r["receivedOn"] == today for r in store_report["rows"]), "báo cáo sách trong kho lọc theo ngày nhận của đơn nhận", text[:300])


def payment_steps(tb, token, tag, card):
    """21b. Thanh toán QR: đúng luồng thật — cấu hình tài khoản nhận tiền, phiếu phạt → khoản thu (sự kiện) → mã QR → webhook đã ký của cổng →
    phiếu phạt đóng + sổ thanh toán có khoản QR. Cổng là giả lập (chữ ký tự tính bằng khoá thử của đơn vị) — cần tài khoản sandbox thật mới kiểm được phía cổng."""
    P, L = "/api/admin/payment", "/api/admin/circulation"
    sepay_key, hash_secret = f"e2e-sepay-{tag}", f"e2e-hash-{tag}"

    def call(method, path, body=None, headers=None, authed=True):
        s, _, text = tb.request(method, path, token=token if authed else None, json_body=body, headers=headers)
        return s, text, (json.loads(text) if text.startswith(("{", "[")) else {})

    s, text, _ = call("PUT", f"{P}/payment-account", {
        "vietQrEnabled": True, "bankBin": "970422", "accountNo": "0123456789", "accountName": "Thu vien E2E", "sepayApiKey": sepay_key, "clearSepayApiKey": False,
        "vnPayEnabled": True, "vnPayTmnCode": "ELIBE2E", "vnPayHashSecret": hash_secret, "clearVnPayHashSecret": False, "vnPayPaymentUrl": None, "expireMinutes": 15})
    expect(s == 200 and sepay_key not in text and hash_secret not in text, "lưu tài khoản nhận tiền (VietQR + VNPAY), bí mật không hiện lại", text[:300])

    def new_ticket(amount):
        for _ in range(30):  # bản sao bạn đọc (patron → circulation) đến qua RabbitMQ — chờ nếu bạn đọc vừa tạo
            s, text, ticket = call("POST", f"{L}/fine-tickets/Add", {"cardNo": card, "amount": amount, "note": f"E2E thanh toán QR {tag}"})
            if s != 404: break
            time.sleep(1)
        expect(s == 201, f"lập phiếu phạt thủ công {ticket.get('code')} ({amount:,}đ)", text[:200])
        return ticket

    def create_payment(ticket, provider):
        for _ in range(30):  # ChargeIssued đi circulation → RabbitMQ → payment
            s, text, pay = call("POST", f"{P}/payments/Create", {"sourceService": "circulation", "sourceId": ticket["publicId"], "provider": provider})
            if s == 201: return pay
            time.sleep(1)
        fail(f"không sinh được mã {provider}: {s} {text[:200]}")

    def ticket_detail(ticket):
        s, text, d = call("GET", f"{L}/fine-tickets/Detail/{ticket['publicId']}")
        return d if s == 200 else {}

    def wait_done(ticket):
        d = {}
        for _ in range(30):
            d = ticket_detail(ticket)
            if d.get("ticket", {}).get("status") == 2: break
            time.sleep(1)
        return d

    # --- VietQR + webhook Sepay
    t1 = new_ticket(50000)
    pay = create_payment(t1, "VietQR")
    expect(pay["qrContent"].startswith("000201") and pay["amount"] == 50000 and pay["transactionCode"] in pay["qrContent"], f"mã VietQR {pay['transactionCode']} đúng số tiền + nội dung", pay["qrContent"][:80])
    hook = {"id": int(time.time()), "gateway": "MBBank", "transactionDate": time.strftime("%Y-%m-%d %H:%M:%S"), "accountNumber": "0123456789", "code": None,
            "content": f"CT DEN {pay['transactionCode'].lower()} E2E", "transferType": "in", "transferAmount": 50000, "referenceCode": f"FT{tag}", "description": "E2E"}
    s, _, _ = call("POST", "/hooks/sepay", hook, headers={"Authorization": "Apikey sai-khoa"}, authed=False)
    expect(s == 401, "webhook Sepay sai khoá bị từ chối (401)")
    s, _, _ = call("POST", "/hooks/sepay", {**hook, "transferAmount": 1000}, headers={"Authorization": f"Apikey {sepay_key}"}, authed=False)
    s2, _, txs = call("POST", f"{P}/payments/Search", {"keyword": pay["transactionCode"], "pageIndex": 1, "pageSize": 5})
    row = (txs.get("page", {}).get("items") or [{}])[0]
    expect(s == 200 and row.get("status") == "Pending" and row.get("needsReview"), "Sepay báo sai số tiền: không ghi nhận, đánh dấu cần đối soát", str(row)[:200])
    s, _, _ = call("POST", "/hooks/sepay", hook, headers={"Authorization": f"Apikey {sepay_key}"}, authed=False)
    expect(s == 200, "webhook Sepay đúng khoá + đúng số tiền")
    d = wait_done(t1)
    qr = [p for p in d.get("payments", []) if p["method"] == "QR"]
    expect(d.get("ticket", {}).get("remaining") == 0 and len(qr) == 1, "phiếu phạt tự hoàn thành, sổ thanh toán có một khoản QR", str(d.get("payments"))[:200])
    call("POST", "/hooks/sepay", hook, headers={"Authorization": f"Apikey {sepay_key}"}, authed=False)
    time.sleep(2)
    d = ticket_detail(t1)
    expect(len([p for p in d.get("payments", []) if p["method"] == "QR"]) == 1, "webhook gửi lại: vẫn chỉ một khoản QR")
    s, _, again = call("POST", f"{P}/payments/Create", {"sourceService": "circulation", "sourceId": t1["publicId"], "provider": "VietQR"})
    expect(s == 409, "phiếu đã thu đủ không sinh thêm mã (CHARGE_CLOSED)", str(again)[:150])

    # --- VNPAY IPN đã ký
    t2 = new_ticket(80000)
    vn = create_payment(t2, "VNPAY")
    expect(vn["qrContent"].startswith("https://") and "vnp_SecureHash=" in vn["qrContent"] and "vnp_Amount=8000000" in vn["qrContent"], "URL VNPAY đã ký, số tiền × 100", vn["qrContent"][:120])

    def ipn(amount, response="00"):
        params = {"vnp_TmnCode": "ELIBE2E", "vnp_TxnRef": vn["transactionCode"], "vnp_Amount": str(amount * 100), "vnp_ResponseCode": response, "vnp_TransactionStatus": response,
                  "vnp_TransactionNo": "14000" + tag[-3:], "vnp_BankCode": "NCB", "vnp_PayDate": time.strftime("%Y%m%d%H%M%S"), "vnp_OrderInfo": "E2E"}
        query = "&".join(f"{k}={urllib.parse.quote_plus(v)}" for k, v in sorted(params.items()))
        sig = __import__("hmac").new(hash_secret.encode(), query.encode(), hashlib.sha512).hexdigest()
        s, _, text = tb.request("GET", f"/hooks/vnpay/ipn?{query}&vnp_SecureHash={sig}")
        return json.loads(text).get("RspCode") if s == 200 else f"HTTP {s}"

    s, _, text = tb.request("GET", f"/hooks/vnpay/ipn?vnp_TxnRef={vn['transactionCode']}&vnp_Amount=8000000&vnp_SecureHash=deadbeef")
    expect(json.loads(text).get("RspCode") == "97", "IPN VNPAY sai chữ ký → 97")
    expect(ipn(1000) == "04", "IPN VNPAY sai số tiền → 04")
    expect(ipn(80000, "24") == "00", "IPN VNPAY khách huỷ → xác nhận, không ghi nhận")
    expect(ticket_detail(t2).get("ticket", {}).get("remaining") == 80000, "phiếu chưa bị thu khi cổng báo thất bại / sai số tiền")
    expect(ipn(80000) == "00", "IPN VNPAY hợp lệ → 00")
    expect(ipn(80000) == "02", "IPN VNPAY gửi lại → 02 (đã xác nhận)")
    d = wait_done(t2)
    expect(d.get("ticket", {}).get("remaining") == 0 and len([p for p in d.get("payments", []) if p["method"] == "QR"]) == 1, "phiếu VNPAY hoàn thành, một khoản QR")

    s, _, totals = call("POST", f"{P}/payments/Search", {"transactionStatus": "Paid", "pageIndex": 1, "pageSize": 50})
    paid_codes = {r["transactionCode"] for r in totals.get("page", {}).get("items", [])}
    expect({pay["transactionCode"], vn["transactionCode"]} <= paid_codes and totals["totals"]["paid"] >= 130000, f"đối soát: đã thu {totals.get('totals', {}).get('paid')}đ trong danh sách giao dịch")
    s, _, logs = call("POST", f"{P}/webhook-logs/Search", {"pageIndex": 1, "pageSize": 20})
    expect(s == 200 and logs.get("totalCount", 0) >= 6 and sepay_key not in json.dumps(logs), "nhật ký webhook có đủ lời gọi, không chứa khoá")


def digital_docs_steps(tb, token, tag):
    """21d. Tài liệu số: trường giàu (tác giả họ/tên, người hướng dẫn, số định danh), bộ lọc nâng cao, thao tác hàng loạt, xuất/nhập, file, phân quyền bộ sưu tập."""
    D = "/api/admin/digital"
    XLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"

    def call(method, path, body=None):
        s, _, text = tb.request(method, path, token=token, json_body=body)
        return s, text, (json.loads(text) if text.startswith(("{", "[")) else {})

    s, text, src = call("POST", f"{D}/collections/Add", {"name": f"Nguồn {tag}"})
    expect(s == 201, "tạo bộ sưu tập nguồn", text[:150])
    s, text, dst = call("POST", f"{D}/collections/Add", {"name": f"Đích {tag}"})
    expect(s == 201, "tạo bộ sưu tập đích", text[:150])

    # --- trường giàu + dữ liệu cũ chỉ có chuỗi phẳng
    s, text, rich = call("POST", f"{D}/items/Add", {
        "title": f"Giáo trình e2e {tag}", "collectionId": src["id"], "publishDate": "2019", "publisher": "NXB E2E",
        "authors": [{"last": "Nguyễn", "first": "Văn A"}, {"last": "Trần", "first": "Thị B"}], "advisors": [{"last": "Lê", "first": "C", "title": "PGS.TS"}],
        "identifiers": [{"type": "isbn", "value": "978-604-e2e"}], "abstract": "Tóm tắt e2e", "journalName": "Tạp chí e2e", "publishMonth": 11, "status": 2})
    expect(s == 201 and rich.get("author") == "Nguyễn Văn A; Trần Thị B" and rich.get("publishYear") == 2019 and len(rich.get("authors", [])) == 2,
           "tài liệu giàu trường: tác giả họ/tên → chuỗi ghép, năm số", text[:250])
    s, text, legacy = call("POST", f"{D}/items/Add", {"title": f"Cũ e2e {tag}", "collectionId": src["id"], "author": "Tác giả cũ", "publishDate": "05/2010", "status": 2})
    expect(s == 201 and legacy["authors"][0]["last"] == "Tác giả cũ" and legacy.get("publishYear") == 2010, "người ghi cũ chỉ gửi chuỗi vẫn có danh sách tác giả và năm", text[:250])

    # --- lọc nâng cao
    s, _, page = call("POST", f"{D}/items/Search", {"author": "trần thị", "publishYearFrom": 2015, "publishYearTo": 2020, "pageSize": 20})
    expect(s == 200 and [i["title"] for i in page["items"]] == [rich["title"]], "lọc theo tác giả + khoảng năm xuất bản", str(page)[:200])
    s, _, page = call("POST", f"{D}/items/Search", {"collectionId": src["id"], "publishYearTo": 2012, "pageSize": 20})
    expect(s == 200 and [i["title"] for i in page["items"]] == [legacy["title"]], "lọc theo năm suy từ chuỗi ngày cũ")

    # --- chọn hàng loạt: theo mã và theo bộ lọc
    s, _, preview = call("POST", f"{D}/items/Selection/Preview", {"search": {"collectionId": src["id"]}})
    expect(s == 200 and preview["count"] == 2, "đếm kết quả lọc làm vùng chọn", str(preview))
    s, text, moved = call("PUT", f"{D}/items/BulkMoveCollection", {"selection": {"publicIds": [rich["publicId"]]}, "collectionId": dst["id"]})
    expect(s == 200 and moved["updated"] == 1, "chuyển bộ sưu tập hàng loạt (theo mã)", text[:150])
    s, _, got = call("GET", f"{D}/items/GetById/{rich['publicId']}")
    expect(got["collectionId"] == dst["id"], "tài liệu đã sang bộ sưu tập đích")

    # --- xuất Excel / metadata
    s, h, xbytes = tb.request("POST", f"{D}/items/Export", token=token, json_body={"selection": {"search": {"collectionId": dst["id"]}}, "fields": ["title", "author", "free"]}, binary=True)
    expect(s == 200 and xbytes[:2] == b"PK" and len(xbytes) > 500, "xuất Excel theo bộ lọc (file .xlsx)", str(s))
    s, _, zbytes = tb.request("POST", f"{D}/items/ExportMetadata", token=token, json_body={"selection": {"publicIds": [rich["publicId"]]}}, binary=True)
    with zipfile.ZipFile(io.BytesIO(zbytes)) as z:
        names = z.namelist()
        dc = z.read(names[0]).decode("utf-8")
    expect(s == 200 and names == ["item_001/dublin_core.xml"] and "Nguyễn Văn A" in dc and "978-604-e2e" in dc, "xuất metadata Dublin Core (ZIP)", str(names))

    # --- nhập Excel: dòng đúng vào, dòng lỗi báo theo dòng, các dòng khác vẫn nhập
    book = xlsx([["Tiêu đề", "Tác giả", "Năm xuất bản", "Ngôn ngữ"], [f"Nhập e2e A {tag}", "X; Y", "2021", "English"], [f"Nhập e2e lỗi {tag}", None, None, "Klingon"], [f"Nhập e2e B {tag}", "Z", "2022", None]])
    s, text, res = call_multipart(tb, token, f"{D}/items/ImportExcel", [("file", "nhap.xlsx", book, XLSX)], {"collectionId": str(dst["id"]), "published": "true"})
    expect(s == 200 and res["imported"] == 2 and res["failed"] == 1 and res["errors"][0].startswith("Dòng 3:"), "nhập Excel: 2 dòng vào, dòng 3 lỗi báo đúng số dòng", text[:250])
    s, _, page = call("POST", f"{D}/items/Search", {"title": f"nhập e2e a {tag}".lower(), "pageSize": 5})
    imported_a = page["items"][0] if page.get("items") else {}
    expect(imported_a.get("language") == "en" and imported_a.get("status") == 2 and len(imported_a.get("authors", [])) == 2, "dòng nhập: ngôn ngữ English → en, xuất bản, 2 tác giả")

    # --- nhập Dublin Core XML: file đúng, file XXE bị từ chối theo tên
    xml_ok = f'<dublin_core><dcvalue element="title" qualifier="none">Nhập XML e2e {tag}</dcvalue><dcvalue element="contributor" qualifier="author">Phạm Văn D</dcvalue></dublin_core>'.encode()
    xml_evil = b'<?xml version="1.0"?><!DOCTYPE x [<!ENTITY e SYSTEM "file:///etc/passwd">]><dublin_core><dcvalue element="title" qualifier="none">&e;</dcvalue></dublin_core>'
    s, text, res = call_multipart(tb, token, f"{D}/items/ImportXml", [("file", "tot.xml", xml_ok, "text/xml"), ("file", "xxe.xml", xml_evil, "text/xml")], {"collectionId": str(dst["id"])})
    expect(s == 200 and res["imported"] == 1 and res["failed"] == 1 and res["errors"][0].startswith("xxe.xml:"), "nhập XML: file thường vào, file có DTD bị từ chối theo tên", text[:250])

    # --- file: thêm bản ghi file → danh sách theo thứ tự → sửa mô tả/thứ tự → xoá
    media = str(uuid.uuid4())
    s, text, f1 = call("POST", f"{D}/files/Add", {"ebookItemPublicId": rich["publicId"], "mediaFileId": media, "type": "Document", "fileExt": "pdf", "description": "Bản chính", "sortOrder": 2})
    expect(s == 201, "thêm file đính kèm", text[:150])
    s, text, f2 = call("PUT", f"{D}/files/Update/{f1['publicId']}", {"ebookItemPublicId": rich["publicId"], "mediaFileId": media, "type": "attachment", "fileExt": "pdf", "description": "Phụ lục", "sortOrder": 1})
    expect(s == 200 and f2["type"] == "attachment" and f2["description"] == "Phụ lục" and f2["sortOrder"] == 1, "sửa loại / mô tả / thứ tự file", text[:150])
    s, _, page = call("POST", f"{D}/items/Search", {"title": rich["title"].lower(), "pageSize": 5})
    expect(page["items"][0]["totalFile"] == 1, "danh sách hiện số file của tài liệu")
    s, text, bulk = call("POST", f"{D}/items/BulkDelete", {"publicIds": [rich["publicId"]]})
    expect(s == 200 and bulk["updated"] == 0 and bulk["skipped"][0]["code"] == "EBOOK_ITEM_HAS_FILES", "xoá hàng loạt bỏ qua tài liệu còn file và nêu lý do", text[:200])
    call("DELETE", f"{D}/files/Delete/{f1['publicId']}")

    # --- phân quyền bộ sưu tập theo loại bạn đọc
    s, _, rts = call("POST", "/api/admin/patron/reader-types/SearchAll", {})
    if s == 200 and rts:
        rt = rts[0]["id"]
        s, text, perms = call("PUT", f"{D}/collections/{dst['publicId']}/permissions", [{"readerTypeId": rt, "read": False, "download": True, "maxDocument": 3, "offlineDays": 7}])
        expect(s == 200 and perms[0]["read"] is True and perms[0]["download"] is True and perms[0]["maxDocument"] == 3, "phân quyền bộ sưu tập: bật tải thì tự bật đọc", text[:200])
        s, _, perms = call("PUT", f"{D}/collections/{dst['publicId']}/permissions", [])
        expect(s == 200 and perms == [], "đặt lại rỗng xoá quyền theo loại")

    # --- dọn: xoá tài liệu thử rồi bộ sưu tập thử
    s, _, everything = call("POST", f"{D}/items/Search", {"pageSize": 100})
    mine = [i["publicId"] for i in everything["items"] if tag in i["title"]]
    if mine: call("POST", f"{D}/items/BulkDelete", {"publicIds": mine})
    call("DELETE", f"{D}/collections/Delete/{src['publicId']}")
    call("DELETE", f"{D}/collections/Delete/{dst['publicId']}")


def make_pdf(text):
    """PDF một trang có lớp văn bản (font chuẩn Helvetica) — đủ để trích chữ; không cần thư viện."""
    objects = [
        "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
    ]
    stream = f"BT /F1 12 Tf 50 750 Td ({text}) Tj ET"
    objects.append(f"<< /Length {len(stream)} >>\nstream\n{stream}\nendstream")
    objects.append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
    out = b"%PDF-1.4\n"
    offsets = []
    for number, body in enumerate(objects, 1):
        offsets.append(len(out))
        out += f"{number} 0 obj\n{body}\nendobj\n".encode()
    xref = len(out)
    out += f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n".encode()
    for offset in offsets:
        out += f"{offset:010d} 00000 n \n".encode()
    return out + f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode()


def shared_data_steps(sysb, sys_token, tb, token, accounts, tag):
    """21g. Đơn vị chủ SHARED (đơn vị thường mã SHARED) → bản sao chỉ đọc ở mọi đơn vị có license; đơn vị ẩn/hiện được."""
    code, sub = "SHARED", "shared"
    s, _, body = sysb.request("GET", "/api/system/tenant/tenants?search=shared", token=sys_token)
    found = [t for t in json.loads(body)["items"] if t["code"] == code]
    if found:
        shared = found[0]; step("đơn vị SHARED đã có từ lần chạy trước")
    else:
        s, _, body = sysb.request("POST", "/api/system/tenant/tenants", token=sys_token, json_body={
            "code": code, "name": "Dữ liệu dùng chung toàn hệ thống", "subdomain": sub, "licenses": [{"moduleCode": m} for m in ["CATALOG", "DIGITAL"]]})
        expect(s == 201, "tạo đơn vị SHARED", body)
        shared = json.loads(body)
    for _ in range(60):
        s, _, body = sysb.request("GET", f"/api/system/tenant/tenants/{shared['publicId']}", token=sys_token)
        shared = json.loads(body)
        if shared["status"] != "Provisioning": break
        time.sleep(1)
    expect(shared["status"] == "Active", "SHARED khởi tạo xong: Active", json.dumps(shared)[:300])
    if "SHARED_ADMIN_PASSWORD" not in accounts and "SHARED_ADMIN_TEMP" not in accounts:
        temp = "Tam" + secrets.token_hex(6) + "7"
        admin_name = "shared-admin"
        s, _, body = sysb.request("POST", f"/api/system/identity/tenants/{shared['id']}/admins", token=sys_token,
                                  json_body={"userName": admin_name, "fullName": "Quản trị dữ liệu dùng chung", "email": None, "password": temp})
        if s != 201:  # tài khoản đã có từ lần chạy dở trước (mật khẩu chưa kịp lưu) → tạo tên khác
            admin_name = "shared-admin-" + secrets.token_hex(2)
            s, _, body = sysb.request("POST", f"/api/system/identity/tenants/{shared['id']}/admins", token=sys_token,
                                      json_body={"userName": admin_name, "fullName": "Quản trị dữ liệu dùng chung", "email": None, "password": temp})
        expect(s == 201, f"tạo tài khoản {admin_name}", body)
        accounts["SHARED_ADMIN_USER"] = admin_name
        accounts["SHARED_ADMIN_TEMP"] = temp
        accounts["SHARED_ADMIN_URL"] = f"https://{sub}.{DOMAIN}/admin/"
        save(accounts)
    host = f"{sub}.{DOMAIN}"
    origin = f"https://{host}"
    sb = Browser(GATEWAY, host=host, proto="https")
    password = accounts.get("SHARED_ADMIN_PASSWORD") or accounts["SHARED_ADMIN_TEMP"]
    stoken = pkce_login(sb, origin, accounts.get("SHARED_ADMIN_USER", "shared-admin"), password)
    s, _, body = sb.request("GET", "/api/me", token=stoken)
    if json.loads(body)["mustChangePassword"]:
        new = "Demo" + secrets.token_hex(6) + "5"
        s, _, body = sb.request("POST", "/api/me/password", token=stoken, json_body={"currentPassword": password, "newPassword": new})
        expect(s == 204, "shared-admin đổi mật khẩu lần đầu", body)
        accounts["SHARED_ADMIN_PASSWORD"] = new
        accounts.pop("SHARED_ADMIN_TEMP", None)
        save(accounts)

    D, C = "/api/admin/digital", "/api/admin/catalog"

    def sj(method, path, body=None):
        st, _, text = sb.request(method, path, token=stoken, json_body=body)
        return st, text, (json.loads(text) if text.startswith(("{", "[")) else {})

    def tj(method, path, body=None):
        st, _, text = tb.request(method, path, token=token, json_body=body)
        return st, text, (json.loads(text) if text.startswith(("{", "[")) else {})

    s, text, _ = tj("GET", f"{D}/shared-sync/Status")
    expect(s == 403 and "NOT_SHARED_TENANT" in text, "đơn vị thường không xem được đồng bộ dữ liệu dùng chung", text[:200])
    s, text, st0 = sj("GET", f"{D}/shared-sync/Status")
    expect(s == 200 and st0.get("isShared") is True, "đơn vị SHARED xem được trạng thái đồng bộ", text[:200])

    title = f"Tài liệu dùng chung e2e {tag}"
    term = f"e2e-chung-{tag}"
    s, text, gitem = sj("POST", f"{D}/items/Add", {"title": title, "author": "Tác giả chung", "free": True, "status": 2})
    expect(s == 201, "SHARED tạo tài liệu số gốc", text[:200])
    s, text, _ = sj("POST", f"{C}/dic-keywords/Add", {"value": term})
    expect(s == 201, "SHARED tạo mục từ điển từ khoá gốc", text[:200])
    for service in ("digital", "catalog"):
        s, text, _ = sj("POST", f"/api/admin/{service}/shared-sync/Run")
        expect(s == 200, f"yêu cầu đồng bộ ngay ({service})", text[:200])

    replica = None
    for _ in range(90):
        s, _, page = tj("POST", f"{D}/items/Search", {"title": title, "pageSize": 5})
        rows = [r for r in page.get("items", []) if r["title"] == title]
        if rows:
            replica = rows[0]; break
        time.sleep(2)
    expect(replica is not None, "đơn vị nhận bản sao tài liệu dùng chung (đồng bộ nền)")
    expect(replica["isReplica"] is True and replica["hiddenByTenant"] is False, "bản sao có cờ Dùng chung, chưa ẩn", json.dumps(replica)[:200])
    s, text, terms = tj("POST", f"{C}/dic-keywords/Search", {"search": term, "pageSize": 5})
    expect(s == 200 and any(t["value"] == term and t["isReplica"] for t in terms.get("items", [])), "đơn vị nhận mục từ điển dùng chung", text[:200])

    s, text, _ = tj("PUT", f"{D}/items/Update/{replica['publicId']}", {"title": "Sửa trộm"})
    expect(s in (403, 409) and "SHARED_READ_ONLY" in text, "đơn vị không sửa được bản sao", f"{s} {text[:200]}")
    s, text, _ = tj("DELETE", f"{D}/items/Delete/{replica['publicId']}")
    expect(s in (403, 409) and "SHARED_READ_ONLY" in text, "đơn vị không xoá được bản sao", f"{s} {text[:200]}")

    s, text, hid = tj("PUT", f"{D}/items/{replica['publicId']}/Hidden", {"hidden": True})
    expect(s == 200 and hid["hiddenByTenant"] is True, "đơn vị ẩn bản sao", text[:200])
    s, _, _ = tb.request("GET", f"/api/opac/digital/browse/items/{replica['publicId']}")
    expect(s == 404, f"OPAC không còn thấy tài liệu đã ẩn ({s})")
    s, text, _ = tj("PUT", f"{D}/items/{replica['publicId']}/Hidden", {"hidden": False})
    expect(s == 200, "đơn vị hiện lại bản sao", text[:200])
    s, _, _ = tb.request("GET", f"/api/opac/digital/browse/items/{replica['publicId']}")
    expect(s == 200, f"OPAC thấy lại tài liệu ({s})")

    # Gốc sửa → bản sao cập nhật; gốc xoá → bản sao ngừng hiển thị
    s, text, _ = sj("PUT", f"{D}/items/Update/{gitem['publicId']}", {"title": title + " (sửa)", "author": "Tác giả chung", "free": True, "status": 2})
    expect(s == 200, "SHARED sửa tài liệu gốc", text[:200])
    time.sleep(6)  # mốc đồng bộ chồng 5 giây: lần chạy kế tiếp mới thấy thay đổi cùng giây
    sj("POST", f"{D}/shared-sync/Run")
    updated = False
    for _ in range(90):
        s, _, page = tj("POST", f"{D}/items/Search", {"title": title + " (sửa)", "pageSize": 5})
        if any(r["title"] == title + " (sửa)" for r in page.get("items", [])):
            updated = True; break
        time.sleep(2)
    expect(updated, "bản sao cập nhật theo tài liệu gốc")

    s, text, _ = sj("DELETE", f"{D}/items/Delete/{gitem['publicId']}")
    expect(s in (200, 204), "SHARED xoá tài liệu gốc", text[:200])
    time.sleep(6)
    sj("POST", f"{D}/shared-sync/Run")
    gone = False
    for _ in range(90):
        s, _, _ = tb.request("GET", f"/api/opac/digital/browse/items/{replica['publicId']}")
        if s == 404:
            gone = True; break
        time.sleep(2)
    expect(gone, "gốc bị xoá → bản sao không còn trên OPAC của đơn vị")


def serials_steps(tb, token, tag, book):
    """21h. Báo – tạp chí: Postgres + RLS thật (khác SQLite của test)."""
    S = "/api/admin/serials"

    def call(method, path, body=None):
        st, _, text = tb.request(method, S + path, token=token, json_body=body)
        return st, text, (json.loads(text) if text[:1] in "{[" else {})

    s, text, freqs = call("POST", "/frequencies/SearchAll", {})
    freqs = freqs if isinstance(freqs, list) else freqs.get("items", [])
    expect(s == 200 and len(freqs) >= 1, "tần suất mặc định đã seed cho đơn vị", text[:200])
    s, text, pats = call("POST", "/patterns/SearchAll", {})
    pats = pats if isinstance(pats, list) else pats.get("items", [])
    expect(s == 200 and len(pats) >= 1, "mẫu đánh số mặc định đã seed", text[:200])
    month = next((f for f in freqs if f.get("unit") == 3), freqs[0])
    title = f"Tạp chí E2E {tag}"
    s, text, sub = call("POST", "/subscriptions/Add", {"title": title, "issn": "1234-5678", "frequencyId": month["id"], "patternId": pats[0]["id"],
                                                          "firstIssueDate": "2026-01-01", "startX": 1, "startY": 1, "graceDays": 0, "status": 1})
    expect(s == 201 and sub.get("publicId"), "tạo đăng ký đặt báo (nhập tay nhan đề/ISSN)", text[:200])
    s, text, gen = call("POST", "/issues/Predict", {"subscriptionPublicId": sub["publicId"], "count": 4})
    expect(s == 200 and gen.get("generated") == 4, "sinh 4 kỳ dự kiến", text[:200])
    s, text, again = call("POST", "/issues/Predict", {"subscriptionPublicId": sub["publicId"], "count": 4})
    expect(s == 200 and again.get("generated") == 4, "sinh tiếp 4 kỳ kế (không trùng số đã có)", text[:200])
    first = gen["issues"][0]["publicId"]
    s, text, _ = call("POST", "/issues/Receive", {"publicId": first})
    expect(s == 200, "nhận kỳ đầu", text[:200])
    s, text, _ = call("POST", "/issues/Claim", {"publicId": first})
    expect(s in (400, 409) and "ISSUE" in text.upper(), "khiếu nại kỳ đã nhận bị chặn", f"{s} {text[:200]}")
    s, text, st = call("GET", f"/issues/Statistics/{sub['publicId']}")
    expect(s == 200 and st.get("total") == 8 and st.get("received") == 1, "thống kê kỳ", text[:200])
    s, text, _ = call("POST", f"/subscriptions/Approve/{sub['publicId']}")
    expect(s in (400, 409) and "SERIAL_BIB_REQUIRED" in text, "duyệt khi chưa liên kết biểu ghi bị chặn", f"{s} {text[:200]}")

    # Đóng tập: biểu ghi catalog → snapshot ở serials → liên kết → nhận 2 kỳ → đóng tập tạo ĐKCB THẬT ở holdings → xoá tập gỡ ĐKCB
    C, H = "/api/admin/catalog", "/api/admin/holdings"
    s, _, body = tb.request("POST", f"{C}/bibs/Add", token=token, json_body={"bibTypeId": book["id"], "fields": [
        {"tag": "245", "ind1": "1", "ind2": "0", "subfields": [{"code": "a", "value": f"Tạp chí đóng tập {tag}"}]}]})
    expect(s == 201, "biên mục biểu ghi đầu báo", body[:200])
    bib = json.loads(body)
    found = []
    for _ in range(40):
        st, text, found = call("POST", "/bibs/Lookup", {"keyword": f"Tạp chí đóng tập {tag}"})
        if st == 200 and isinstance(found, list) and any(b.get("mfn") == bib["mfn"] for b in found): break
        time.sleep(1)
    expect(isinstance(found, list) and any(b.get("mfn") == bib["mfn"] for b in found), "biểu ghi vào bản sao của serials (BibChanged)", str(found)[:200])
    s, text, _ = call("POST", f"/subscriptions/LinkBib/{sub['publicId']}/{bib['publicId']}")
    expect(s == 200, "liên kết đăng ký với biểu ghi", text[:200])
    for issue in gen["issues"][1:3]:
        s, text, _ = call("POST", "/issues/Receive", {"publicId": issue["publicId"]})
        expect(s == 200, "nhận kỳ để đóng tập", text[:200])
    accession = f"TC-{tag}"
    s, text, binding = call("POST", "/bindings/Add", {"subscriptionPublicId": sub["publicId"], "issueIds": [i["publicId"] for i in gen["issues"][:3]],
                                                      "accessionNo": accession, "volumeTitle": f"Tập 1 {tag}"})
    expect(s == 201 and binding.get("publicId"), "đóng tập 3 kỳ đã nhận", text[:300])
    s, _, body = tb.request("POST", f"{H}/items/Search", token=token, json_body={"barcodeFrom": accession, "barcodeTo": accession, "pageSize": 5})
    items = json.loads(body).get("items", []) if s == 200 else []
    expect(any(i.get("barcode") == accession for i in items), "ĐKCB của tập đóng có thật ở holdings (qua service token)", body[:300])
    s, text, _ = call("POST", "/bindings/Add", {"subscriptionPublicId": sub["publicId"], "issueIds": [gen["issues"][0]["publicId"]], "accessionNo": accession + "-2"})
    expect(s in (400, 409), "một kỳ chỉ thuộc một tập", f"{s} {text[:200]}")
    s, text, _ = call("DELETE", f"/bindings/Delete/{binding['publicId']}")
    expect(s in (200, 204), "xoá tập đóng", text[:200])
    s, _, body = tb.request("POST", f"{H}/items/Search", token=token, json_body={"barcodeFrom": accession, "barcodeTo": accession, "pageSize": 5})
    expect(s == 200 and not any(i.get("barcode") == accession for i in json.loads(body).get("items", [])), "ĐKCB của tập đã gỡ khỏi holdings")
    s, text, rep = call("POST", "/reports/ReceivedSummary", {"pageIndex": 1, "pageSize": 20})
    expect(s == 200, "báo cáo tổng hợp nhận", text[:200])
    s, text, _ = call("GET", "/dashboard")
    expect(s == 200 and text.startswith("{"), "dashboard serials", text[:200])
    s, text, _ = call("DELETE", f"/subscriptions/Delete/{sub['publicId']}")
    expect(s in (200, 204), "xoá đăng ký thử", text[:200])


def digital_content_steps(tb, token, tag):
    """21e. Tra ISBN, gợi ý AI, đồng bộ nội dung PDF (trích chữ chạy nền) → tìm trong nội dung ở OPAC, gói DSpace kèm file xuất → nhập lại."""
    D = "/api/admin/digital"

    def call(method, path, body=None, authed=True):
        s, _, text = tb.request(method, path, token=token if authed else None, json_body=body)
        return s, text, (json.loads(text) if text.startswith(("{", "[")) else {})

    s, text, caps = call("GET", f"{D}/items/Capabilities")
    expect(s == 200 and caps.get("isbn") is True and caps.get("content") is True, "khả năng tích hợp của màn tài liệu số", text[:200])
    print(f"  [THÔNG TIN]  AI gợi ý: {'bật' if caps.get('ai') else 'tắt (chưa cấu hình DIGITAL_AI_API_KEY)'}; vector hóa: {'bật' if caps.get('embedding') else 'tắt (chưa cấu hình DIGITAL_OLLAMA_URL)'}")

    s, text, _ = call("GET", f"{D}/items/LookupIsbn?isbn=123")
    expect(s == 400 and "ISBN_INVALID" in text, "tra ISBN sai định dạng bị từ chối", text[:200])
    s, text, book = call("GET", f"{D}/items/LookupIsbn?isbn=9780747532699")
    print(f"  [{'OK' if s == 200 and book.get('title') else 'CẢNH BÁO'}]  tra ISBN 9780747532699 (Google Books/Open Library): {book.get('title') if s == 200 else 'không ra được Internet / không có kết quả'}")
    s, text, _ = call("POST", f"{D}/items/SuggestClassification", {"title": "Kinh tế vi mô", "description": "Giáo trình"})
    expect((s == 200) if caps.get("ai") else (s == 503 and "AI_DISABLED" in text), "gợi ý AI: " + ("có phản hồi" if caps.get("ai") else "tắt khi chưa cấu hình khóa"), text[:200])

    # --- file PDF thật: xin vé upload media → PUT → hoàn tất → tạo tài liệu và gắn file
    phrase = f"noi dung e2e {tag} marginal utility"
    pdf = make_pdf(phrase)
    s, _, body = tb.request("POST", "/api/admin/media/files/uploads", token=token,
                            json_body={"purpose": "ebook-file", "fileName": "e2e.pdf", "contentType": "application/pdf", "size": len(pdf)})
    ticket = json.loads(body) if s == 200 else {}
    expect(s == 200 and ticket.get("fileId"), "xin vé upload file tài liệu số", body[:200])
    tb.request("PUT", ticket.get("uploadUrl", "/x"), raw=(pdf, "application/pdf"))
    s, _, body = tb.request("POST", f"/api/admin/media/files/{ticket.get('fileId')}/complete", token=token)
    expect(s == 200, "hoàn tất upload PDF", body[:200])

    title = f"Nội dung PDF e2e {tag}"
    s, text, item = call("POST", f"{D}/items/Add", {"title": title, "author": "Tác giả nội dung", "free": True, "status": 2})
    expect(s == 201, "tạo tài liệu để đồng bộ nội dung", text[:200])
    s, text, attached = call("POST", f"{D}/files/Add", {"ebookItemPublicId": item["publicId"], "mediaFileId": ticket["fileId"], "type": "Document", "fileExt": "pdf", "fileSize": len(pdf)})
    expect(s == 201, "gắn file PDF vào tài liệu", text[:200])

    # --- đồng bộ nội dung: việc nền trích chữ (chu kỳ ~20 giây)
    s, text, queued = call("POST", f"{D}/items/SyncContent", {"publicIds": [item["publicId"]]})
    expect(s == 200 and queued.get("queued") == 1, "đưa tài liệu vào hàng đợi đồng bộ nội dung", text[:200])
    status = {}
    for _ in range(40):
        s, text, rows = call("POST", f"{D}/items/ContentStatus", {"publicIds": [item["publicId"]]})
        status = rows[0] if s == 200 and rows else {}
        if status.get("status") in ("done", "failed", "no_text"):
            break
        time.sleep(3)
    expect(status.get("status") == "done" and status.get("chunkCount", 0) >= 1 and status.get("pageCount") == 1,
           f"đồng bộ nội dung: {status.get('status')} ({status.get('chunkCount')} đoạn, {status.get('embeddedCount')} có vector) {status.get('error') or ''}", str(status)[:300])

    s, text, hits = call("POST", "/api/opac/digital/browse/content/search", {"q": f"e2e {tag} marginal"}, authed=False)
    mine = [h for h in hits if h.get("publicId") == item["publicId"]] if s == 200 and isinstance(hits, list) else []
    expect(mine and mine[0].get("page") == 1 and "marginal utility" in (mine[0].get("snippet") or ""), "OPAC: tìm trong nội dung thấy tài liệu kèm trang và đoạn trích", text[:300])
    s, text, _ = call("POST", "/api/opac/digital/browse/content/search", {"q": "x"}, authed=False)
    expect(s == 400 and "CONTENT_QUERY_INVALID" in text, "tìm trong nội dung: câu tìm quá ngắn bị từ chối", text[:200])

    # --- xuất gói DSpace kèm file rồi nhập lại
    s, h, packed = tb.request("POST", f"{D}/items/ExportWithFiles", token=token, json_body={"selection": {"publicIds": [item["publicId"]]}}, binary=True)
    with zipfile.ZipFile(io.BytesIO(packed)) as z:
        names = z.namelist()
        pdf_back = z.read("item_001/document.pdf") if "item_001/document.pdf" in names else b""
    expect(s == 200 and pdf_back == pdf and "item_001/dublin_core.xml" in names and "item_001/contents" in names, "xuất gói DSpace kèm file (PDF đúng từng byte)", str(names))
    s, text, imported = call_multipart(tb, token, f"{D}/items/ImportDspace", [("file", "goi.zip", packed, "application/zip")], {"published": "true"})
    expect(s == 200 and imported.get("imported") == 1 and imported.get("failed") == 0, "nhập gói DSpace (metadata + PDF)", text[:300])
    s, text, found = call("POST", f"{D}/items/Search", {"title": title.lower(), "pageSize": 10})
    copies = [i for i in found.get("items", []) if i["publicId"] != item["publicId"]] if s == 200 else []
    expect(len(copies) == 1 and copies[0].get("totalFile") == 1, "tài liệu nhập từ gói có đúng 1 file", text[:300])

    # --- dọn: xoá file rồi tài liệu (tài liệu còn file thì không xoá được)
    for it in [item] + copies:
        s, text, files = call("POST", f"{D}/files/Search", {"ebookItemPublicId": it["publicId"], "pageSize": 20})
        for f in files.get("items", []) if s == 200 else []:
            call("DELETE", f"{D}/files/Delete/{f['publicId']}")
    call("POST", f"{D}/items/BulkDelete", {"publicIds": [item["publicId"]] + [c["publicId"] for c in copies]})


def call_multipart(tb, token, path, files, fields):
    s, _, text = tb.request("POST", path, token=token, raw=multipart_fields(files, fields))
    return s, text, (json.loads(text) if text.startswith(("{", "[")) else {})


def digital_portal_steps(tb, token, tag):
    """21c. Luồng thật qua gateway: digital (đánh giá, ebook) → search (chỉ mục ebook qua RabbitMQ) và portal (tin đăng, menu, liên hệ ẩn danh)."""
    D, P = "/api/admin/digital", "/api/admin/portal"

    def call(method, path, body=None, authed=True):
        s, _, text = tb.request(method, path, token=token if authed else None, json_body=body)
        return s, text, (json.loads(text) if text.startswith(("{", "[")) else {})

    # --- ebook: tạo, xuất bản → search lập chỉ mục (không dấu) trong vài giây
    title = f"Sách điện tử thử nghiệm {tag}"
    s, text, item = call("POST", f"{D}/items/Add", {"title": title, "author": "Tác giả E2E", "free": True, "status": 2})
    expect(s == 201 and item.get("publicId"), "tạo tài liệu số xuất bản", text[:200])
    found = []
    for _ in range(40):
        s, _, res = call("POST", "/api/opac/search/ebooks/Search", {"q": f"sach dien tu thu nghiem {tag}"}, authed=False)
        found = res.get("items", []) if s == 200 else []
        if found: break
        time.sleep(1)
    expect(len(found) == 1 and found[0]["publicId"] == item["publicId"], "ebook vào chỉ mục tra cứu (event EbookChanged → search), tìm không dấu", str(found)[:200])

    # --- tìm gộp in + số (một danh sách): số theo loại, lọc loại, trang 2 (kiểm cách Npgsql dịch thứ hạng)
    s, _, allr = call("POST", "/api/opac/search/all/Search", {"q": f"sach dien tu thu nghiem {tag}", "pageSize": 1}, authed=False)
    expect(s == 200 and allr.get("digitalTotal") == 1 and allr["items"][0]["kind"] == "digital" and allr["items"][0]["publicId"] == item["publicId"],
           "tìm gộp thấy tài liệu số", str(allr)[:200])
    s, _, only = call("POST", "/api/opac/search/all/Search", {"q": f"sach dien tu thu nghiem {tag}", "kind": "print"}, authed=False)
    expect(s == 200 and only.get("total") == 0 and only.get("digitalTotal") == 1, "tìm gộp lọc loại in: không có, số tài liệu số vẫn đếm", str(only)[:200])
    s, _, p2 = call("POST", "/api/opac/search/all/Search", {"q": "", "sort": "newest", "page": 2, "pageSize": 1}, authed=False)
    expect(s == 200 and p2.get("page") == 2, "tìm gộp trang 2 (sắp mới nhất) chạy được", str(p2)[:200])

    # --- đánh giá: ẩn danh gửi → chờ duyệt → ChangeStatus chung bị chặn → Duyệt → hiện công khai
    s, _, _ = call("POST", f"/api/opac/digital/browse/items/{item['publicId']}/reviews", {"rating": 5, "displayName": "Bạn đọc E2E", "content": "Hay"}, authed=False)
    expect(s in (200, 201), "gửi đánh giá ẩn danh")
    s, _, pub = call("GET", f"/api/opac/digital/browse/items/{item['publicId']}/reviews", authed=False)
    expect(s == 200 and pub == [], "đánh giá mới chưa hiện công khai")
    s, _, rev = call("POST", f"{D}/reviews/Search", {"pageIndex": 1, "pageSize": 5})
    review = rev["items"][0]
    s, text, _ = call("PUT", f"{D}/reviews/ChangeStatus", {"publicId": review["publicId"], "status": 2})
    expect(s == 400 and "REVIEW_USE_APPROVE" in text, "ChangeStatus chung không duyệt được đánh giá (phải qua Approve)", text[:150])
    s, text, _ = call("POST", f"{D}/reviews/{review['publicId']}/Approve")
    expect(s == 200, "duyệt đánh giá", text[:150])
    s, _, pub = call("GET", f"/api/opac/digital/browse/items/{item['publicId']}/reviews", authed=False)
    expect(s == 200 and len(pub) == 1, "đánh giá đã duyệt hiện công khai")

    # --- cổng thông tin: tin đăng, HTML làm sạch, menu mặc định theo saga
    s, text, news = call("POST", f"{P}/news/Add", {"title": f"Tin thử nghiệm {tag}", "content": "<p>Xin chào</p><script>alert(1)</script><img src=x onerror=alert(2)>", "status": 2, "allowComment": True})
    expect(s == 201 and news.get("content") == "<p>Xin chào</p>", "tin đăng: HTML mã độc bị làm sạch ở server", text[:200])
    s, _, detail = call("GET", f"/api/opac/portal/news/{news['slug']}", authed=False)
    expect(s == 200 and "<script" not in (detail.get("content") or ""), "OPAC đọc tin ẩn danh, không có script")
    s, _, menus = call("GET", "/api/opac/portal/menus", authed=False)
    expect(s == 200 and {m["code"] for m in menus} >= {"main", "footer"}, "menu mặc định do portal tự seed khi tạo đơn vị", str(menus)[:150])
    s, _, page = call("POST", "/api/opac/portal/news/Search", {"language": "vi", "page": 1, "pageSize": 5, "q": f"thu nghiem {tag}"}, authed=False)
    expect(s == 200 and any(i["slug"] == news["slug"] for i in page.get("items", [])), "tìm tin không dấu trên OPAC")

    # --- liên hệ ẩn danh → cán bộ thấy → đánh dấu đã xử lý
    s, _, _ = call("POST", "/api/opac/portal/contacts", {"name": "Bạn đọc E2E", "email": "e2e@example.com", "title": f"Hỏi {tag}", "content": "Thư viện mở cửa mấy giờ?"}, authed=False)
    expect(s == 202, "gửi liên hệ ẩn danh", str(s))
    s, _, listing = call("POST", f"{P}/contacts/Search", {"keyword": tag, "pageIndex": 1, "pageSize": 5})
    expect(s == 200 and listing.get("totalCount", 0) >= 1, "cán bộ thấy liên hệ mới")
    s, _, handled = call("POST", f"{P}/contacts/{listing['items'][0]['publicId']}/Handle", {"handleNote": "Đã trả lời"})
    expect(s == 200 and handled.get("status") == 2, "đánh dấu liên hệ đã xử lý")

    # --- dọn: ẩn tin + gỡ ebook để không làm bẩn đơn vị thử
    call("PUT", f"{P}/news/ChangeStatus", {"publicId": news["publicId"], "status": 1})
    call("PUT", f"{D}/items/ChangeStatus", {"publicId": item["publicId"], "status": 1})


if __name__ == "__main__":
    main()
