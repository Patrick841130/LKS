using System.Text.RegularExpressions;

namespace LksBrothers.Explorer.Services
{
    public class SearchService
    {
        private readonly Random _random = new();

        public SearchResult Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new SearchResult
                {
                    Query = query,
                    Type = "invalid",
                    Success = false,
                    Message = "Search query cannot be empty"
                };
            }

            var searchType = DetermineSearchType(query);
            
            return searchType switch
            {
                "block" => SearchBlock(query),
                "transaction" => SearchTransaction(query),
                "address" => SearchAddress(query),
                "validator" => SearchValidator(query),
                _ => SearchGeneral(query)
            };
        }

        private string DetermineSearchType(string query)
        {
            query = query.Trim();

            // Block height (numeric)
            if (Regex.IsMatch(query, @"^\d+$"))
                return "block";

            // Transaction hash (0x followed by 64 hex characters)
            if (Regex.IsMatch(query, @"^0x[a-fA-F0-9]{64}$"))
                return "transaction";

            // LKS address (starts with lks1)
            if (query.StartsWith("lks1"))
                return "address";

            // Validator search
            if (query.ToLower().Contains("validator"))
                return "validator";

            return "general";
        }

        private SearchResult SearchBlock(string query)
        {
            if (!long.TryParse(query, out var blockHeight))
            {
                return new SearchResult
                {
                    Query = query,
                    Type = "block",
                    Success = false,
                    Message = "Invalid block height"
                };
            }

            if (blockHeight < 0 || blockHeight > 2_847_500)
            {
                return new SearchResult
                {
                    Query = query,
                    Type = "block",
                    Success = false,
                    Message = "Block not found"
                };
            }

            return new SearchResult
            {
                Query = query,
                Type = "block",
                Success = true,
                Data = new
                {
                    height = blockHeight,
                    hash = GenerateHash(),
                    timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(0, 1440)),
                    transactions = _random.Next(1500, 3000),
                    validator = $"lks1validator{_random.Next(1, 4)}...{GenerateShortHash()}",
                    size = $"{_random.Next(800, 1200)} KB"
                }
            };
        }

        private SearchResult SearchTransaction(string query)
        {
            return new SearchResult
            {
                Query = query,
                Type = "transaction",
                Success = true,
                Data = new
                {
                    hash = query,
                    timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(0, 60)),
                    from = $"lks1{GenerateShortHash()}...{GenerateShortHash()}",
                    to = $"lks1{GenerateShortHash()}...{GenerateShortHash()}",
                    amount = $"{_random.NextDouble() * 10000:F2} LKS",
                    fee = "$0.00",
                    status = "Confirmed",
                    blockHeight = 2_847_392 - _random.Next(0, 100)
                }
            };
        }

        private SearchResult SearchAddress(string query)
        {
            return new SearchResult
            {
                Query = query,
                Type = "address",
                Success = true,
                Data = new
                {
                    address = query,
                    balance = $"{_random.NextDouble() * 100000:F2} LKS",
                    transactions = _random.Next(10, 1000),
                    firstSeen = DateTime.UtcNow.AddDays(-_random.Next(1, 365)),
                    lastActivity = DateTime.UtcNow.AddHours(-_random.Next(1, 24)),
                    type = query.Contains("validator") ? "Validator" : "Regular"
                }
            };
        }

        private SearchResult SearchValidator(string query)
        {
            var validators = new[]
            {
                new { name = "LKS Validator 1", address = "lks1validator1staking7q8r" },
                new { name = "LKS Validator 2", address = "lks1validator2staking9k3t" },
                new { name = "LKS Validator 3", address = "lks1validator3staking2m7p" }
            };

            var matches = validators.Where(v => 
                v.name.ToLower().Contains(query.ToLower()) || 
                v.address.ToLower().Contains(query.ToLower())).ToList();

            return new SearchResult
            {
                Query = query,
                Type = "validator",
                Success = matches.Any(),
                Data = matches.Select(v => new
                {
                    name = v.name,
                    address = v.address,
                    stake = "16,666,666,667 LKS",
                    uptime = $"{99.96 + _random.NextDouble() * 0.04:F2}%",
                    status = "Active"
                })
            };
        }

        private SearchResult SearchGeneral(string query)
        {
            var results = new List<object>();

            // Search in blocks
            if (query.ToLower().Contains("block"))
            {
                results.Add(new
                {
                    type = "block",
                    title = "Latest Blocks",
                    description = "Recent blockchain blocks",
                    url = "/blocks"
                });
            }

            // Search in transactions
            if (query.ToLower().Contains("transaction") || query.ToLower().Contains("tx"))
            {
                results.Add(new
                {
                    type = "transaction",
                    title = "Latest Transactions",
                    description = "Recent blockchain transactions",
                    url = "/transactions"
                });
            }

            // Search validators
            if (query.ToLower().Contains("validator") || query.ToLower().Contains("stake"))
            {
                results.Add(new
                {
                    type = "validator",
                    title = "Validators",
                    description = "Network validators and staking information",
                    url = "/validators"
                });
            }

            // Search analytics
            if (query.ToLower().Contains("analytics") || query.ToLower().Contains("stats"))
            {
                results.Add(new
                {
                    type = "analytics",
                    title = "Network Analytics",
                    description = "Blockchain performance metrics and statistics",
                    url = "/analytics"
                });
            }

            return new SearchResult
            {
                Query = query,
                Type = "general",
                Success = results.Any(),
                Data = results,
                Message = results.Any() ? null : "No results found"
            };
        }

        public IEnumerable<SearchSuggestion> GetSearchSuggestions(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Enumerable.Empty<SearchSuggestion>();

            var suggestions = new List<SearchSuggestion>();

            // Block suggestions
            if (query.All(char.IsDigit))
            {
                suggestions.Add(new SearchSuggestion
                {
                    Text = $"Block #{query}",
                    Type = "block",
                    Description = "Search for block by height"
                });
            }

            // Address suggestions
            if (query.StartsWith("lks1"))
            {
                suggestions.Add(new SearchSuggestion
                {
                    Text = query,
                    Type = "address",
                    Description = "LKS address"
                });
            }

            // Transaction suggestions
            if (query.StartsWith("0x"))
            {
                suggestions.Add(new SearchSuggestion
                {
                    Text = query,
                    Type = "transaction",
                    Description = "Transaction hash"
                });
            }

            // General suggestions
            var generalSuggestions = new[]
            {
                new { text = "validators", desc = "View all network validators" },
                new { text = "analytics", desc = "Network performance analytics" },
                new { text = "latest blocks", desc = "Recent blockchain blocks" },
                new { text = "latest transactions", desc = "Recent transactions" }
            };

            foreach (var suggestion in generalSuggestions)
            {
                if (suggestion.text.Contains(query.ToLower()))
                {
                    suggestions.Add(new SearchSuggestion
                    {
                        Text = suggestion.text,
                        Type = "general",
                        Description = suggestion.desc
                    });
                }
            }

            return suggestions.Take(5);
        }

        private string GenerateHash()
        {
            var bytes = new byte[32];
            _random.NextBytes(bytes);
            return "0x" + Convert.ToHexString(bytes).ToLower();
        }

        private string GenerateShortHash()
        {
            var bytes = new byte[4];
            _random.NextBytes(bytes);
            return Convert.ToHexString(bytes).ToLower();
        }
    }

    public class SearchResult
    {
        public string Query { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Success { get; set; }
        public object? Data { get; set; }
        public string? Message { get; set; }
    }

    public class SearchSuggestion
    {
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
