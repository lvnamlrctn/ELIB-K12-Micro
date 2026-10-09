"""CLI backfill: python reindex.py --id-from 100 --id-to 200 [--force]

Chạy song song với .NET (EbookIndexingJob vẫn tự động index ebook mới qua Hangfire) —
dùng Postgres advisory lock để tránh đụng độ khi trùng ebook.
"""
import argparse
import datetime
import logging
import sys

import db
import elastic
import minio_client
from config import load_config
from embedding import embed_document
from ocr import ocr_pdf
from pdf_extract import extract_chunks

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("reindex")


def process_item(conn, minio_c, cfg, item_id: int, force: bool) -> None:
    item = db.fetch_item(conn, item_id)
    if item is None:
        log.warning("Bỏ qua item %s: không tìm thấy hoặc đã xóa", item_id)
        return

    file_version = item["FileVersion"] or 1

    if file_version > 1 or force:
        elastic.delete_chunks_by_ebook_id(cfg, item_id)

    pdf_file = db.fetch_pdf_file(conn, item_id)
    meta_values = db.fetch_metadata_values(conn, item_id)
    dc = db.build_dublin_core(meta_values, item["Author"])

    page_chunks = []
    ebook_file_id = None
    if pdf_file is not None:
        ebook_file_id = pdf_file["Id"]
        pdf_bytes = minio_client.download_pdf(minio_c, cfg, pdf_file["Url"])
        page_chunks = extract_chunks(pdf_bytes, cfg.chunk_size, cfg.chunk_overlap)
        if not page_chunks:
            page_chunks = ocr_pdf(cfg, pdf_bytes, cfg.chunk_size, cfg.chunk_overlap)

    if not page_chunks:
        # Không có PDF, hoặc PDF rỗng/OCR thất bại → 1 chunk metadata-only (giữ đúng
        # hành vi .NET: luôn có ít nhất 1 document ES cho mỗi ebook).
        class _Empty:
            page_number = 0
            chunk_index = 0
            text = ""

        page_chunks = [_Empty()]

    now_iso = datetime.datetime.utcnow().isoformat() + "Z"
    docs_with_ids = []
    for pc in page_chunks:
        embedding_vec = None
        if pc.text.strip():
            try:
                embedding_vec = embed_document(cfg, pc.text)
            except Exception as ex:
                log.warning("Embedding thất bại cho item %s trang %s chunk %s: %s",
                            item_id, pc.page_number, pc.chunk_index, ex)

        doc, chunk_id = elastic.build_chunk_doc(
            ebook_id=item_id,
            public_id=str(item["PublicId"]),
            page_number=pc.page_number,
            chunk_index=pc.chunk_index,
            content=pc.text,
            file_version=file_version,
            indexed_at=now_iso,
            ebook_file_id=ebook_file_id,
            title=item["Title"], author=item["Author"], publisher=item["Publisher"],
            publish_date=item["PublishDate"], keyword=item["Keyword"], images=item["Images"],
            collection_id=item["CollectionId"], collection_name=item["CollectionName"],
            topic_id=item["TopicId"], topic_name=item["TopicName"],
            subject_id=item["SubjectId"], subject_name=item["SubjectName"],
            free=item["Free"] == 1, share=item["Share"] == 1, tenant_id=item["TenantId"],
            dc_subject=dc["dc_subject"], dc_description=dc["dc_description"],
            dc_language=dc["dc_language"], dc_identifier=dc["dc_identifier"],
            dc_type=dc["dc_type"], dc_contributor=dc["dc_contributor"],
            embedding=embedding_vec,
        )
        docs_with_ids.append((doc, chunk_id))

    elastic.bulk_upsert(cfg, docs_with_ids)
    db.update_item_success(conn, item_id, file_version + 1)
    log.info("Item %s: index xong %d chunk", item_id, len(docs_with_ids))


def main() -> None:
    parser = argparse.ArgumentParser(description="Backfill index ebook (PDF -> chunk -> embed -> ES)")
    parser.add_argument("--id-from", type=int, required=True)
    parser.add_argument("--id-to", type=int, required=True)
    parser.add_argument("--force", action="store_true", help="Index lại cả ebook đã có IndexedAt")
    args = parser.parse_args()

    cfg = load_config()
    conn = db.connect(cfg)
    minio_c = minio_client.get_client(cfg)

    item_ids = db.fetch_items_to_process(conn, args.id_from, args.id_to, args.force)
    log.info("Tìm thấy %d ebook cần xử lý (id %d..%d, force=%s)", len(item_ids), args.id_from, args.id_to, args.force)

    success, failed, skipped = 0, 0, 0
    for item_id in item_ids:
        with db.advisory_lock(conn, item_id) as acquired:
            if not acquired:
                log.info("Item %s đang được xử lý nơi khác, bỏ qua", item_id)
                skipped += 1
                continue
            try:
                process_item(conn, minio_c, cfg, item_id, args.force)
                success += 1
            except Exception as ex:
                log.exception("Item %s lỗi: %s", item_id, ex)
                try:
                    db.update_item_error(conn, item_id, str(ex))
                except Exception:
                    conn.rollback()
                failed += 1

    log.info("Hoàn thành: %d thành công, %d lỗi, %d bỏ qua (đang khoá)", success, failed, skipped)
    conn.close()
    sys.exit(1 if failed > 0 else 0)


if __name__ == "__main__":
    main()
