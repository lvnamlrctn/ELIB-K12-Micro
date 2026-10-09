"""Trích chunk text từ PDF — port từ PdfExtractorService.cs (PdfPig) sang PyMuPDF.

Thuật toán chunking (SplitIntoChunks) giữ nguyên logic 100% so với .NET; việc tách từ
theo trang dùng PyMuPDF nên kết quả không byte-identical với PdfPig, nhưng tương đương
về mặt ngữ nghĩa (word-level, theo thứ tự đọc).
"""
from dataclasses import dataclass

import fitz  # PyMuPDF


@dataclass
class PageChunk:
    page_number: int
    chunk_index: int
    text: str


def split_into_chunks(words: list[str], chunk_size: int, overlap: int) -> list[str]:
    if len(words) <= chunk_size:
        return [" ".join(words)]

    step = chunk_size - overlap
    if step <= 0:
        step = chunk_size

    chunks: list[str] = []
    start = 0
    while start < len(words):
        slice_ = words[start:start + chunk_size]
        if len(slice_) < 100 and chunks:
            chunks[-1] = chunks[-1] + " " + " ".join(slice_)
            break
        chunks.append(" ".join(slice_))
        if start + chunk_size >= len(words):
            break
        start += step
    return chunks


def extract_chunks(pdf_bytes: bytes, chunk_size: int, overlap: int) -> list[PageChunk]:
    result: list[PageChunk] = []
    doc = fitz.open(stream=pdf_bytes, filetype="pdf")
    try:
        for page in doc:
            try:
                words_raw = page.get_text("words")  # [(x0,y0,x1,y1,text,block,line,word), ...]
                words = [w[4] for w in words_raw if w[4].strip()]
                if not words:
                    text = page.get_text("text") or ""
                    words = [w for w in text.split() if w]
                if not words:
                    continue

                for i, chunk_text in enumerate(split_into_chunks(words, chunk_size, overlap)):
                    result.append(PageChunk(page_number=page.number + 1, chunk_index=i, text=chunk_text))
            except Exception:
                # Trang lỗi cấu trúc (font/XObject hỏng) → bỏ qua trang đó, không dừng cả file
                continue
    finally:
        doc.close()
    return result


def render_pages_as_png(pdf_bytes: bytes, dpi: int = 150) -> list[tuple[int, bytes]]:
    """Trả về [(page_number 1-based, png_bytes), ...] — dùng cho OCR khi PDF không có text layer."""
    pages: list[tuple[int, bytes]] = []
    doc = fitz.open(stream=pdf_bytes, filetype="pdf")
    try:
        zoom = dpi / 72
        matrix = fitz.Matrix(zoom, zoom)
        for page in doc:
            pix = page.get_pixmap(matrix=matrix)
            pages.append((page.number + 1, pix.tobytes("png")))
    finally:
        doc.close()
    return pages
