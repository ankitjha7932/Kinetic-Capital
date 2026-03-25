using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest req)
    {
        // Pass 'true' if register hai to 
        bool isReg = req.Flow?.ToLower() == "register";

        var res = await _auth.SendOtpAsync(req.Email, isReg);

        return res.Success ? Ok(new { message = res.Message }) : BadRequest(res.Message);
    }

    [HttpPost("verify-otp-register")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
    {
        var res = await _auth.VerifyOtpAsync(req.Email, req.Otp);
        return res.Success
            ? Ok(new { token = res.Token, userId = res.UserId })
            : BadRequest(res.Message);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var res = await _auth.LoginAsync(req.Email, req.Password);
        return res.Success
            ? Ok(new { token = res.Token, userId = res.UserId })
            : Unauthorized(res.Message);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var res = await _auth.ForgotPasswordAsync(req.Email);
        return res.Success ? Ok(new { message = res.Message }) : BadRequest(res.Message);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var res = await _auth.ResetPasswordAsync(req.Token, req.NewPassword);
        return res.Success ? Ok(new { message = res.Message }) : BadRequest(res.Message);
    }
}
