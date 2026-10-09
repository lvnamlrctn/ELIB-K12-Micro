# Server Z39.50 của ELIB — Zebra (Index Data).
# Chỉ đọc thư mục MARC ISO2709 do ELIBAPI xuất ra (volume dùng chung), KHÔNG kết nối CSDL.
FROM ubuntu:24.04

# Kho gói chính thức của Index Data (idzebra không có trong kho Ubuntu).
RUN apt-get update && apt-get install -y --no-install-recommends \
        wget gnupg ca-certificates && \
    mkdir -p /usr/share/keyrings && \
    wget -O /usr/share/keyrings/indexdata.gpg https://ftp.indexdata.com/debian/indexdata.gpg && \
    printf 'Types: deb\nURIs: https://ftp.indexdata.com/ubuntu/\nSuites: noble\nComponents: main\nSigned-By: /usr/share/keyrings/indexdata.gpg\n' \
        > /etc/apt/sources.list.d/indexdata.sources && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
        idzebra-2.0 idzebra-2.0-utils libidzebra-2.0-mod-grs-marc && \
    apt-get purge -y wget gnupg && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /zebra
COPY zebra.cfg ./zebra.cfg
COPY tab/      ./tab/
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh

# sed: gỡ CR nếu file được checkout trên Windows với CRLF — bash trong container sẽ lỗi "bad interpreter".
RUN sed -i 's/\r$//' /usr/local/bin/docker-entrypoint.sh && \
    chmod +x /usr/local/bin/docker-entrypoint.sh && \
    mkdir -p /export /var/lib/zebra/reg /var/lib/zebra/shadow /var/lib/zebra/lock \
             /var/lib/zebra/tmp /var/lib/zebra/key

EXPOSE 2100
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
