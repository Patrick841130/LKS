using Microsoft.AspNetCore.Mvc;
using LksBrothers.Explorer.Services;

namespace LksBrothers.Explorer.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly SearchService _searchService;

        public SearchController(SearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet]
        public IActionResult Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search query is required" });
            }

            var result = _searchService.Search(q);
            return Ok(result);
        }

        [HttpGet("suggestions")]
        public IActionResult GetSuggestions([FromQuery] string q)
        {
            var suggestions = _searchService.GetSearchSuggestions(q);
            return Ok(suggestions);
        }
    }
}
