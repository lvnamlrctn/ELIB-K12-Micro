using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

// Chỉ [Authorize], không gắn [Permission]: đây là lookup đọc, không có tác dụng phụ, dùng chung cho cả
// màn hình Mượn sách lẫn Check-in/out — quyền thao tác thật đã được kiểm tra ở hành động mượn/trả theo
// sau lần gọi này.
[Authorize]
[ApiController]
[Route("api/PrintBook/FaceRecognition")]
public class FaceRecognitionController(IFaceRecognitionService faceRecognition) : BaseApiController
{
    [HttpPost("Identify")]
    public async Task<IActionResult> Identify([FromBody] IdentifyFaceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(ApiResponse<string>.Fail("ImageBase64 is required"));

        var result = await faceRecognition.IdentifyReaderAsync(FaceImage.StripDataUrl(request.ImageBase64), GetTenantId());
        return Ok(result != null
            ? ApiResponse<FaceMatchResult?>.Ok(result, "Đã nhận diện")
            : ApiResponse<FaceMatchResult?>.Ok(null, "Không nhận diện được"));
    }
}
