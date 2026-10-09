"""OCR qua Ollama vision model (glm-ocr) — dùng khi PDF không có text layer (scan ảnh).

Khác .NET (gọi Gemini Vision với nguyên file PDF, mất PageNumber): ở đây render từng
trang thành ảnh riêng rồi OCR từng trang, nên giữ được đúng PageNumber thật.
"""
import base64

import requests

from config import Config
from pdf_extract import PageChunk, render_pages_as_png, split_into_chunks

OCR_PROMPT = (
    "Extract all text content from this image. Return plain text only, "
    "preserving paragraph breaks with newlines. No explanations, no markdown formatting."
)


def ocr_page(cfg: Config, png_bytes: bytes) -> str:
    b64 = base64.b64encode(png_bytes).decode("ascii")
    resp = requests.post(
        f"{cfg.ollama_base_url}/api/chat",
        json={
            "model": cfg.ollama_ocr_model,
            "stream": False,
            "messages": [{"role": "user", "content": OCR_PROMPT, "images": [b64]}],
        },
        timeout=180,
    )
    resp.raise_for_status()
    return resp.json()["message"]["content"]


def ocr_pdf(cfg: Config, pdf_bytes: bytes, chunk_size: int, overlap: int) -> list[PageChunk]:
    result: list[PageChunk] = []
    for page_number, png_bytes in render_pages_as_png(pdf_bytes):
        try:
            text = ocr_page(cfg, png_bytes)
        except Exception:
            continue
        words = [w for w in text.split() if w]
        if not words:
            continue
        for i, chunk_text in enumerate(split_into_chunks(words, chunk_size, overlap)):
            result.append(PageChunk(page_number=page_number, chunk_index=i, text=chunk_text))
    return result
