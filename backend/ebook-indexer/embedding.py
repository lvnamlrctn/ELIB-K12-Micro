"""Port chính xác OllamaEmbeddingService.cs: prefix theo tác vụ, num_ctx=8192,
co ngắn khi vượt context length, chuẩn hoá L2 (ES dùng similarity dot_product)."""
import requests

from config import Config

MAX_SHRINK_ATTEMPTS = 6
NUM_CTX = 8192


def _normalize(vec: list[float]) -> list[float]:
    norm = sum(x * x for x in vec) ** 0.5
    return [x / norm for x in vec] if norm > 0 else vec


def _embed_internal(cfg: Config, prefix: str, text: str) -> list[float]:
    for attempt in range(MAX_SHRINK_ATTEMPTS):
        resp = requests.post(
            f"{cfg.ollama_base_url}/api/embeddings",
            json={
                "model": cfg.ollama_embedding_model,
                "prompt": prefix + text,
                "options": {"num_ctx": NUM_CTX},
            },
            timeout=60,
        )
        if resp.ok:
            vec = resp.json()["embedding"]
            return _normalize(vec)

        body = resp.text
        if "exceeds the context length" in body and attempt < MAX_SHRINK_ATTEMPTS - 1:
            text = text[: len(text) // 2]
            continue
        raise RuntimeError(f"Ollama embeddings API {resp.status_code}: {body}")

    raise RuntimeError("Vượt quá số lần thử co ngắn prompt do context length")


def embed_document(cfg: Config, text: str) -> list[float]:
    return _embed_internal(cfg, "search_document: ", text)


def embed_query(cfg: Config, text: str) -> list[float]:
    return _embed_internal(cfg, "search_query: ", text)
