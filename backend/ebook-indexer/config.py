"""Cấu hình — load .env và giải mã giá trị ENC: (cùng thuật toán AesEncryptionHelper.cs của .NET)."""
import os
import base64
from dataclasses import dataclass

from dotenv import load_dotenv
from Crypto.Cipher import AES
from Crypto.Util.Padding import pad, unpad

load_dotenv()

# Phải khớp chính xác Key/IV hard-code trong ELIBAPI.Core/Common/AesEncryptionHelper.cs
_AES_KEY = b"ELIBAPI-MinIO-SecretKey-32Bytes!"
_AES_IV = b"ELIBAPI-IV-16By!"


def decrypt(value: str | None) -> str:
    """Giải mã giá trị có prefix ENC:, hoặc trả nguyên nếu không có prefix."""
    if not value:
        return ""
    if not value.startswith("ENC:"):
        return value
    cipher_bytes = base64.b64decode(value[4:])
    cipher = AES.new(_AES_KEY, AES.MODE_CBC, _AES_IV)
    plain = unpad(cipher.decrypt(cipher_bytes), AES.block_size)
    return plain.decode("utf-8")


def encrypt(value: str) -> str:
    """Mã hoá plaintext thành chuỗi ENC:... (đối xứng với decrypt())."""
    cipher = AES.new(_AES_KEY, AES.MODE_CBC, _AES_IV)
    cipher_bytes = cipher.encrypt(pad(value.encode("utf-8"), AES.block_size))
    return "ENC:" + base64.b64encode(cipher_bytes).decode("ascii")


def _bool(name: str, default: str = "false") -> bool:
    return os.environ.get(name, default).strip().lower() in ("1", "true", "yes")


@dataclass(frozen=True)
class Config:
    pg_host: str
    pg_port: int
    pg_database: str
    pg_user: str
    pg_password: str

    minio_endpoint: str
    minio_access_key: str
    minio_secret_key: str
    minio_private_bucket: str
    minio_use_ssl: bool

    es_uri: str
    es_username: str
    es_password: str
    es_chunk_index_name: str

    ollama_base_url: str
    ollama_embedding_model: str
    ollama_ocr_model: str

    chunk_size: int
    chunk_overlap: int


def load_config() -> Config:
    return Config(
        pg_host=os.environ["PG_HOST"],
        pg_port=int(os.environ.get("PG_PORT", "5432")),
        pg_database=os.environ["PG_DATABASE"],
        pg_user=os.environ["PG_USER"],
        pg_password=decrypt(os.environ.get("PG_PASSWORD", "")),

        minio_endpoint=os.environ["MINIO_ENDPOINT"],
        minio_access_key=os.environ["MINIO_ACCESS_KEY"],
        minio_secret_key=decrypt(os.environ.get("MINIO_SECRET_KEY", "")),
        minio_private_bucket=os.environ.get("MINIO_PRIVATE_BUCKET", "elibapi-private"),
        minio_use_ssl=_bool("MINIO_USE_SSL"),

        es_uri=os.environ["ES_URI"].rstrip("/"),
        es_username=os.environ.get("ES_USERNAME", ""),
        es_password=decrypt(os.environ.get("ES_PASSWORD", "")),
        es_chunk_index_name=os.environ.get("ES_CHUNK_INDEX_NAME", "ebook_chunks"),

        ollama_base_url=os.environ.get("OLLAMA_BASE_URL", "http://localhost:11434").rstrip("/"),
        ollama_embedding_model=os.environ.get("OLLAMA_EMBEDDING_MODEL", "nomic-embed-text"),
        ollama_ocr_model=os.environ.get("OLLAMA_OCR_MODEL", "glm-ocr:latest"),

        chunk_size=int(os.environ.get("CHUNK_SIZE", "500")),
        chunk_overlap=int(os.environ.get("CHUNK_OVERLAP", "50")),
    )


if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2:
        print("Dùng: python config.py \"plaintext-can-ma-hoa\"")
        sys.exit(1)
    print(encrypt(sys.argv[1]))
