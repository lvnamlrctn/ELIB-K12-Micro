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
Chạy trên máy DEV: python3 e2e_gd0.py   (đọc secret từ .env cùng thư mục; mật khẩu tài khoản demo ghi vào demo-accounts.txt, quyền 600)
  - sysadmin đã đổi mật khẩu bắt buộc: ELIB_SYSADMIN_PASSWORD='...' python3 e2e_gd0.py
  - không có mật khẩu sysadmin: python3 e2e_gd0.py --tenant-only  (bỏ bước 1–3 và phần cần quản trị nền tảng)
Bước 4 gọi gateway qua 127.0.0.1:<GATEWAY_PORT> với Host của đơn vị (chưa có DNS wildcard), coi như đi qua nginx https.
"""
import base64, hashlib, html, http.cookiejar, io, json, os, re, secrets, sys, time, urllib.error, urllib.parse, urllib.request, zipfile
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

    def request(self, method, path, data=None, token=None, json_body=None, raw=None, binary=False):
        headers = {}
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

    print("2. Tạo đơn vị + khởi tạo")
    s, _, body = sysb.request("GET", "/api/system/tenant/tenants?search=" + TENANT_CODE.lower(), token=sys_token)
    existing = [t for t in json.loads(body)["items"] if t["code"] == TENANT_CODE]
    if existing:
        tenant = existing[0]; step(f"đơn vị {TENANT_CODE} đã có từ lần chạy trước")
    else:
        s, _, body = sysb.request("POST", "/api/system/tenant/tenants", token=sys_token, json_body={
            "code": TENANT_CODE, "name": "Trường demo nền tảng mới", "subdomain": SUBDOMAIN,
            "licenses": [{"moduleCode": m} for m in ["CATALOG", "HOLDINGS", "CIRCULATION", "SEARCH"]]})
        expect(s == 201, f"tạo đơn vị {TENANT_CODE}", body)
        tenant = json.loads(body)
    for _ in range(60):
        s, _, body = sysb.request("GET", f"/api/system/tenant/tenants/{tenant['publicId']}", token=sys_token)
        tenant = json.loads(body)
        if tenant["status"] != "Provisioning": break
        time.sleep(1)
    steps = ", ".join(f"{p['service']}={p['status']}" for p in tenant["provisioningSteps"])
    expect(tenant["status"] == "Active", f"saga khởi tạo xong: Active ({steps})", json.dumps(tenant))
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
    expect(s == 200 and sorted(json.loads(body)["modules"]) == ["CATALOG", "CIRCULATION", "HOLDINGS", "SEARCH"],
           f"menu theo license: {json.loads(body).get('modules')}", body)
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


if __name__ == "__main__":
    main()
