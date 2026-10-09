"""Truy vấn Postgres trực tiếp (raw SQL, không ORM) — schema "Ebook"."""
from contextlib import contextmanager

import psycopg2
import psycopg2.extras

from config import Config

# FieldId của MetaDataValue dùng cho Dublin Core (giữ nguyên như EbookIndexingJob.cs)
FIELD_DC_SUBJECT = 57
FIELD_DC_DESCRIPTION = 27
FIELD_DC_LANGUAGE = 38
FIELD_DC_IDENTIFIER_ISBN = 20
FIELD_DC_IDENTIFIER_ISSN = 23
FIELD_DC_TYPE = 66
FIELD_DC_CONTRIBUTOR = 3


def connect(cfg: Config):
    return psycopg2.connect(
        host=cfg.pg_host, port=cfg.pg_port, dbname=cfg.pg_database,
        user=cfg.pg_user, password=cfg.pg_password,
        cursor_factory=psycopg2.extras.RealDictCursor,
    )


def fetch_items_to_process(conn, id_from: int, id_to: int, force: bool) -> list[int]:
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT "Id" FROM "Ebook"."Item"
            WHERE "IsDelete" != 2 AND "Id" >= %s AND "Id" < %s
              AND (%s OR "IndexedAt" IS NULL)
            ORDER BY "Id"
            """,
            (id_from, id_to, force),
        )
        return [row["Id"] for row in cur.fetchall()]


@contextmanager
def advisory_lock(conn, item_id: int):
    """Postgres advisory lock theo Item.Id — tránh đụng độ với .NET đang index cùng ebook."""
    with conn.cursor() as cur:
        cur.execute("SELECT pg_try_advisory_lock(%s)", (item_id,))
        acquired = cur.fetchone()["pg_try_advisory_lock"]
    try:
        yield acquired
    finally:
        if acquired:
            with conn.cursor() as cur:
                cur.execute("SELECT pg_advisory_unlock(%s)", (item_id,))


def fetch_item(conn, item_id: int) -> dict | None:
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT
                i."Id", i."CollectionId", i."SubjectId", i."TopicId", i."Images",
                i."Free", i."Share", i."FileVersion", i."TenantId", i."PublicId",
                x."Title", x."Author", x."Publisher", x."PublishDate", x."Keyword",
                c."Name" AS "CollectionName",
                t."Name" AS "TopicName",
                s."Name" AS "SubjectName"
            FROM "Ebook"."Item" i
            LEFT JOIN "Ebook"."itemXml" x ON x."Id" = i."Id"
            LEFT JOIN "Ebook"."collection" c ON c."Id" = i."CollectionId"
            LEFT JOIN "Ebook"."Topic" t ON t."Id" = i."TopicId"
            LEFT JOIN "Ebook"."Subject" s ON s."Id" = i."SubjectId"
            WHERE i."Id" = %s AND i."IsDelete" != 2
            """,
            (item_id,),
        )
        return cur.fetchone()


def fetch_pdf_file(conn, item_id: int) -> dict | None:
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT "Id", "Url" FROM "Ebook"."EbookFile"
            WHERE "EbookId" = %s AND "IsDelete" != 2
              AND ("FileExt" = 'pdf' OR "FileExt" = '.pdf' OR "FileType" = 'application/pdf')
            ORDER BY "SortOrder" ASC, "Id" DESC
            LIMIT 1
            """,
            (item_id,),
        )
        return cur.fetchone()


def fetch_metadata_values(conn, item_id: int) -> list[dict]:
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT "MetaDataFieldId", "Value", "SortOrder" FROM "Ebook"."MetaDataValue"
            WHERE "ItemId" = %s AND "IsDelete" != 2
            ORDER BY "SortOrder"
            """,
            (item_id,),
        )
        return cur.fetchall()


def build_dublin_core(meta_values: list[dict], author_fallback: str | None) -> dict:
    def first(field_id: int) -> str | None:
        for m in meta_values:
            if m["MetaDataFieldId"] == field_id and m["Value"]:
                return m["Value"]
        return None

    def joined(field_id: int) -> str | None:
        vals = [m["Value"] for m in meta_values if m["MetaDataFieldId"] == field_id and m["Value"]]
        return "; ".join(vals) if vals else None

    dc_contributor = joined(FIELD_DC_CONTRIBUTOR) or author_fallback

    return {
        "dc_subject": joined(FIELD_DC_SUBJECT),
        "dc_description": first(FIELD_DC_DESCRIPTION),
        "dc_language": first(FIELD_DC_LANGUAGE),
        "dc_identifier": first(FIELD_DC_IDENTIFIER_ISBN) or first(FIELD_DC_IDENTIFIER_ISSN),
        "dc_type": first(FIELD_DC_TYPE),
        "dc_contributor": dc_contributor,
    }


def update_item_success(conn, item_id: int, new_file_version: int) -> None:
    with conn.cursor() as cur:
        cur.execute(
            """
            UPDATE "Ebook"."Item"
            SET "IndexedAt" = now(), "FileVersion" = %s, "ErrorMessage" = NULL
            WHERE "Id" = %s
            """,
            (new_file_version, item_id),
        )
    conn.commit()


def update_item_error(conn, item_id: int, message: str) -> None:
    with conn.cursor() as cur:
        cur.execute(
            """UPDATE "Ebook"."Item" SET "ErrorMessage" = %s WHERE "Id" = %s""",
            (message[:1000], item_id),
        )
    conn.commit()
