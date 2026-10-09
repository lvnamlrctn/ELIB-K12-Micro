"""Bulk upsert + delete_by_query lên Elasticsearch — dùng đúng field camelCase khớp
mapping thật (ebookId/ebookFileId: long, publicId: keyword) như EbookChunkDocument.cs."""
import json

import requests

from config import Config

BATCH_SIZE = 200


def _auth(cfg: Config):
    return (cfg.es_username, cfg.es_password) if cfg.es_username else None


def delete_chunks_by_ebook_id(cfg: Config, ebook_id: int) -> None:
    resp = requests.post(
        f"{cfg.es_uri}/{cfg.es_chunk_index_name}/_delete_by_query",
        auth=_auth(cfg),
        json={"query": {"term": {"ebookId": ebook_id}}},
        timeout=30,
    )
    resp.raise_for_status()


def build_chunk_doc(
    *, ebook_id: int, public_id: str, page_number: int, chunk_index: int, content: str,
    file_version: int, indexed_at: str, ebook_file_id: int | None,
    title, author, publisher, publish_date, keyword, images,
    collection_id, collection_name, topic_id, topic_name, subject_id, subject_name,
    free: bool, share: bool, tenant_id,
    dc_subject, dc_description, dc_language, dc_identifier, dc_type, dc_contributor,
    embedding: list[float] | None,
) -> dict:
    chunk_id = f"{ebook_id}_p{page_number:04d}_c{chunk_index:02d}"
    doc = {
        "chunkId": chunk_id,
        "ebookId": ebook_id,
        "publicId": public_id,
        "pageNumber": page_number,
        "chunkIndex": chunk_index,
        "content": content,
        "fileVersion": file_version,
        "indexedAt": indexed_at,
        "ebookFileId": ebook_file_id,
        "title": title,
        "author": author,
        "publisher": publisher,
        "publishDate": publish_date,
        "keyword": keyword,
        "images": images,
        "collectionId": str(collection_id) if collection_id is not None else None,
        "collectionName": collection_name,
        "topicId": str(topic_id) if topic_id is not None else None,
        "topicName": topic_name,
        "subjectId": str(subject_id) if subject_id is not None else None,
        "subjectName": subject_name,
        "free": free,
        "share": share,
        "tenantId": tenant_id,
        "dcSubject": dc_subject,
        "dcDescription": dc_description,
        "dcLanguage": dc_language,
        "dcIdentifier": dc_identifier,
        "dcType": dc_type,
        "dcContributor": dc_contributor,
    }
    if embedding is not None:
        doc["embedding"] = embedding
    return doc, chunk_id


def bulk_upsert(cfg: Config, docs_with_ids: list[tuple[dict, str]]) -> None:
    for i in range(0, len(docs_with_ids), BATCH_SIZE):
        batch = docs_with_ids[i:i + BATCH_SIZE]
        lines = []
        for doc, chunk_id in batch:
            lines.append(json.dumps({"index": {"_index": cfg.es_chunk_index_name, "_id": chunk_id}}))
            lines.append(json.dumps(doc))
        body = "\n".join(lines) + "\n"

        resp = requests.post(
            f"{cfg.es_uri}/_bulk",
            auth=_auth(cfg),
            headers={"Content-Type": "application/x-ndjson"},
            data=body.encode("utf-8"),
            timeout=60,
        )
        resp.raise_for_status()
        result = resp.json()
        if result.get("errors"):
            first_error = next(
                (item["index"]["error"] for item in result["items"] if "error" in item.get("index", {})),
                None,
            )
            raise RuntimeError(f"Bulk index thất bại cho {cfg.es_chunk_index_name}: {first_error}")
