using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/[controller]")]
public abstract class PublicBaseController : ControllerBase { }
