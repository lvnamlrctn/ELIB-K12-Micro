"""Tải PDF từ MinIO bucket private (EbookFile.Url là object name, không phải URL đầy đủ)."""
import io

from minio import Minio

from config import Config


def get_client(cfg: Config) -> Minio:
    return Minio(
        cfg.minio_endpoint,
        access_key=cfg.minio_access_key,
        secret_key=cfg.minio_secret_key,
        secure=cfg.minio_use_ssl,
    )


def download_pdf(client: Minio, cfg: Config, object_name: str) -> bytes:
    resp = client.get_object(cfg.minio_private_bucket, object_name)
    try:
        buf = io.BytesIO()
        for chunk in resp.stream(64 * 1024):
            buf.write(chunk)
        return buf.getvalue()
    finally:
        resp.close()
        resp.release_conn()
