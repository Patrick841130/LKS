using Microsoft.AspNetCore.Mvc;
using LksBrothers.Explorer.Services;

namespace LksBrothers.Explorer.Controllers
{
    [ApiController]
    [Route("api/validators")]
    public class ValidatorsController : ControllerBase
    {
        private readonly ValidatorService _validatorService;

        public ValidatorsController(ValidatorService validatorService)
        {
            _validatorService = validatorService;
        }

        [HttpGet]
        public IActionResult GetValidators()
        {
            var validators = _validatorService.GetAllValidators();
            return Ok(validators);
        }

        [HttpGet("{address}")]
        public IActionResult GetValidator(string address)
        {
            var validator = _validatorService.GetValidator(address);
            if (validator == null)
            {
                return NotFound(new { message = "Validator not found" });
            }
            return Ok(validator);
        }

        [HttpGet("stats")]
        public IActionResult GetValidatorStats()
        {
            var stats = _validatorService.GetValidatorStats();
            return Ok(stats);
        }

        [HttpGet("performance")]
        public IActionResult GetValidatorPerformance([FromQuery] string? address = null, [FromQuery] int days = 7)
        {
            var performance = _validatorService.GetValidatorPerformance(address, days);
            return Ok(performance);
        }

        [HttpPost("{address}/status")]
        public IActionResult UpdateValidatorStatus(string address, [FromBody] UpdateStatusRequest request)
        {
            _validatorService.UpdateValidatorStatus(address, request.Status);
            return Ok(new { message = "Status updated successfully" });
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
