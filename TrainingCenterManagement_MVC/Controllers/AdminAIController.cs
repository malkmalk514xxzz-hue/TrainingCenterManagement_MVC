using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text.Json;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminAIController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory   _httpFactory;
        private readonly IMemoryCache         _cache;
        private readonly IConfiguration       _configuration;

        private const string ConfigCacheKey = "AI:SystemConfig";

        public AdminAIController(ApplicationDbContext context,
                                 IHttpClientFactory httpFactory,
                                 IMemoryCache cache,
                                 IConfiguration configuration)
        {
            _context       = context;
            _httpFactory   = httpFactory;
            _cache         = cache;
            _configuration = configuration;
        }

        // ── Settings ─────────────────────────────────────────────────

        public async Task<IActionResult> Settings()
        {
            var config = await GetOrCreateConfigAsync();

            // Resolve effective key: DB key first, then appsettings.json fallback
            var effectiveAnthropic = config.AnthropicApiKey ?? _configuration["Anthropic:ApiKey"] ?? "";
            var effectiveOpenAI    = config.OpenAIApiKey    ?? _configuration["OpenAI:ApiKey"]    ?? "";
            var effectiveGroq      = config.GroqApiKey      ?? _configuration["Groq:ApiKey"]      ?? "";

            ViewBag.AnthropicKeyConfigured = IsValidKey(effectiveAnthropic, "sk-ant-");
            ViewBag.OpenAIKeyConfigured    = IsValidKey(effectiveOpenAI,    "sk-");
            ViewBag.GroqKeyConfigured      = IsValidKey(effectiveGroq,      "gsk_");

            // Pass masked versions to view (show last 4 chars only)
            ViewBag.AnthropicKeyMasked = MaskKey(effectiveAnthropic);
            ViewBag.OpenAIKeyMasked    = MaskKey(effectiveOpenAI);
            ViewBag.GroqKeyMasked      = MaskKey(effectiveGroq);

            // Whether key lives in DB (editable) vs appsettings (read-only hint)
            ViewBag.AnthropicKeyInDb = !string.IsNullOrWhiteSpace(config.AnthropicApiKey);
            ViewBag.OpenAIKeyInDb    = !string.IsNullOrWhiteSpace(config.OpenAIApiKey);
            ViewBag.GroqKeyInDb      = !string.IsNullOrWhiteSpace(config.GroqApiKey);

            return View(config);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(AISystemConfig model)
        {
            var config             = await GetOrCreateConfigAsync();
            config.Provider        = model.Provider;
            config.OllamaUrl       = string.IsNullOrWhiteSpace(model.OllamaUrl)
                                     ? "http://localhost:11434" : model.OllamaUrl.Trim();
            config.OllamaModel     = string.IsNullOrWhiteSpace(model.OllamaModel)
                                     ? "llama3.2" : model.OllamaModel.Trim();
            config.OpenAIModel     = string.IsNullOrWhiteSpace(model.OpenAIModel)
                                     ? "gpt-4o-mini" : model.OpenAIModel.Trim();
            config.GroqModel       = string.IsNullOrWhiteSpace(model.GroqModel)
                                     ? "llama-3.3-70b-versatile" : model.GroqModel.Trim();
            config.MaxTokensPerResponse = Math.Clamp(model.MaxTokensPerResponse, 256, 4096);
            config.SystemDailyLimit     = model.SystemDailyLimit <= 0 ? -1 : model.SystemDailyLimit;
            config.IsEnabled       = model.IsEnabled;
            config.UpdatedAt       = DateTime.UtcNow;
            config.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Save API keys only when a real (non-masked) value is submitted
            config.AnthropicApiKey = ResolveKeyUpdate(model.AnthropicApiKey, config.AnthropicApiKey);
            config.OpenAIApiKey    = ResolveKeyUpdate(model.OpenAIApiKey,    config.OpenAIApiKey);
            config.GroqApiKey      = ResolveKeyUpdate(model.GroqApiKey,      config.GroqApiKey);

            await _context.SaveChangesAsync();
            _cache.Remove(ConfigCacheKey);

            TempData["AISettingsSuccess"] = "تم حفظ إعدادات المساعد الذكي بنجاح.";
            return RedirectToAction(nameof(Settings));
        }

        // ── AJAX: verify API key ──────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyKey([FromForm] string provider, [FromForm] string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Contains('•'))
                return Json(new { success = false, message = "المفتاح غير صالح" });

            try
            {
                var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(8);

                switch (provider.ToLower())
                {
                    case "anthropic":
                    {
                        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                        {
                            Content = new StringContent(
                                """{"model":"claude-haiku-4-5-20251001","max_tokens":10,"messages":[{"role":"user","content":"Hi"}]}""",
                                System.Text.Encoding.UTF8, "application/json")
                        };
                        req.Headers.Add("x-api-key", key);
                        req.Headers.Add("anthropic-version", "2023-06-01");
                        var resp = await http.SendAsync(req);
                        return resp.IsSuccessStatusCode
                            ? Json(new { success = true,  message = "✓ المفتاح صالح ويعمل بشكل مثالي" })
                            : Json(new { success = false, message = $"✗ المفتاح غير صالح ({(int)resp.StatusCode})" });
                    }
                    case "openai":
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                        req.Headers.Add("Authorization", $"Bearer {key}");
                        var resp = await http.SendAsync(req);
                        return resp.IsSuccessStatusCode
                            ? Json(new { success = true,  message = "✓ المفتاح صالح ويعمل بشكل مثالي" })
                            : Json(new { success = false, message = $"✗ المفتاح غير صالح ({(int)resp.StatusCode})" });
                    }
                    case "groq":
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.groq.com/openai/v1/models");
                        req.Headers.Add("Authorization", $"Bearer {key}");
                        var resp = await http.SendAsync(req);
                        return resp.IsSuccessStatusCode
                            ? Json(new { success = true,  message = "✓ المفتاح صالح ويعمل بشكل مثالي" })
                            : Json(new { success = false, message = $"✗ المفتاح غير صالح ({(int)resp.StatusCode})" });
                    }
                    default:
                        return Json(new { success = false, message = "مزود غير معروف" });
                }
            }
            catch (TaskCanceledException)
            {
                return Json(new { success = false, message = "انتهت مهلة الاتصال" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ── AJAX: fetch Ollama models ─────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> OllamaModels(string url = "http://localhost:11434")
        {
            try
            {
                var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(6);
                var response = await http.GetAsync($"{url.TrimEnd('/')}/api/tags");
                if (!response.IsSuccessStatusCode)
                    return Json(new { success = false, message = "تعذّر الاتصال بـ Ollama — تحقق من عنوان URL" });

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var models = doc.RootElement.GetProperty("models")
                    .EnumerateArray()
                    .Select(m => m.GetProperty("name").GetString())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();

                return Json(new { success = true, models });
            }
            catch (TaskCanceledException)
            {
                return Json(new { success = false, message = "انتهت مهلة الاتصال — تأكد من تشغيل Ollama" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ── AJAX: all models (installed + popular catalog) ───────────

        [HttpGet]
        public async Task<IActionResult> OllamaAllModels(string url = "http://localhost:11434")
        {
            var installed  = new List<string>();
            string? connErr = null;

            try
            {
                var http     = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(6);
                var resp     = await http.GetAsync($"{url.TrimEnd('/')}/api/tags");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    installed = doc.RootElement.GetProperty("models")
                        .EnumerateArray()
                        .Select(m => m.GetProperty("name").GetString()!)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();
                }
                else connErr = "تعذّر الاتصال بـ Ollama";
            }
            catch (TaskCanceledException) { connErr = "انتهت مهلة الاتصال — تأكد من تشغيل Ollama"; }
            catch (Exception ex)          { connErr = ex.Message; }

            static bool IsMatch(string a, string b)
            {
                string aBase = a.Split(':')[0], aTag = a.Contains(':') ? a.Split(':')[1] : "latest";
                string bBase = b.Split(':')[0], bTag = b.Contains(':') ? b.Split(':')[1] : "latest";
                return string.Equals(aBase, bBase, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(aTag,  bTag,  StringComparison.OrdinalIgnoreCase);
            }

            var popular = new[]
            {
                // ── أفضل للعربية ──────────────────────────────────────────────────
                new { name = "qwen2.5:7b",       size = "4.4 GB",  desc = "Alibaba • الأفضل للعربية، يتبع التعليمات بدقة",          tag = "⭐ عربي"  },
                new { name = "qwen2.5:14b",      size = "8.7 GB",  desc = "Alibaba • أقوى نسخ qwen للعربية",                         tag = "⭐ عربي"  },
                new { name = "qwen2.5:3b",       size = "1.9 GB",  desc = "Alibaba • خفيف وسريع، عربي جيد",                          tag = "عربي"    },
                new { name = "aya:8b",           size = "4.8 GB",  desc = "Cohere • مُصمَّم للغات غير الإنجليزية بما فيها العربية",  tag = "عربي"    },
                new { name = "aya-expanse:8b",   size = "4.7 GB",  desc = "Cohere • نسخة محسّنة من aya للمحادثات",                   tag = "عربي"    },
                new { name = "hermes3",          size = "4.7 GB",  desc = "Nous Research • ممتاز في اتباع التعليمات والأدوار",        tag = "ذكي"     },
                new { name = "hermes3:8b",       size = "4.7 GB",  desc = "Nous Research • نسخة 8B من hermes3",                      tag = "ذكي"     },
                // ── نماذج عامة قوية ──────────────────────────────────────────────
                new { name = "llama3.2",         size = "2.0 GB",  desc = "Meta • سريع للمهام اليومية",                              tag = "موصى به" },
                new { name = "llama3.1",         size = "4.7 GB",  desc = "Meta • أداء عالٍ ومنطق قوي",                             tag = "قوي"     },
                new { name = "llama3.1:8b",      size = "4.7 GB",  desc = "Meta • جودة ممتازة، حجم متوسط",                          tag = "قوي"     },
                new { name = "mistral",          size = "4.1 GB",  desc = "Mistral AI • متعدد اللغات",                               tag = "متعدد"   },
                new { name = "gemma2",           size = "5.5 GB",  desc = "Google • جودة عالية ومتوازنة",                            tag = "قوي"     },
                new { name = "gemma2:2b",        size = "1.6 GB",  desc = "Google • النسخة الخفيفة جداً",                            tag = "خفيف"    },
                new { name = "phi3",             size = "2.3 GB",  desc = "Microsoft • كفء وصغير الحجم",                             tag = "خفيف"    },
                new { name = "phi3.5",           size = "2.2 GB",  desc = "Microsoft • أحدث إصدار من phi وأدق",                      tag = "خفيف"    },
                new { name = "deepseek-r1",      size = "4.7 GB",  desc = "DeepSeek • تفكير عميق وتحليل",                            tag = "تفكير"   },
                new { name = "deepseek-r1:8b",   size = "4.9 GB",  desc = "DeepSeek • نسخة 8B، تحليل متقدم",                         tag = "تفكير"   },
                new { name = "codellama",        size = "3.8 GB",  desc = "Meta • متخصص في البرمجة",                                 tag = "كود"     },
                new { name = "llama3.2:1b",      size = "1.3 GB",  desc = "Meta • النسخة الخفيفة جداً",                              tag = "خفيف"    },
            };

            return Json(new
            {
                success  = connErr == null,
                error    = connErr,
                installed,
                popular  = popular.Select(p => new
                {
                    p.name, p.size, p.desc, p.tag,
                    isInstalled = installed.Any(i => IsMatch(i, p.name))
                })
            });
        }

        // ── SSE: stream pull (download) progress ──────────────────────

        [HttpGet]
        public async Task PullModel(string url, string model)
        {
            var buf = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
            buf?.DisableBuffering();

            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Append("Connection",        "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no");

            async Task EmitAsync(object data)
            {
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(data)}\n\n");
                await Response.Body.FlushAsync();
            }

            try
            {
                var http     = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromMinutes(30);

                var payload = JsonSerializer.Serialize(new { name = model, stream = true });
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{url.TrimEnd('/')}/api/pull")
                {
                    Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
                };

                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead,
                                                       HttpContext.RequestAborted);
                if (!resp.IsSuccessStatusCode)
                {
                    await EmitAsync(new { error = $"فشل الاتصال بـ Ollama ({(int)resp.StatusCode})" });
                    return;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
                using var reader = new System.IO.StreamReader(stream);

                while (!reader.EndOfStream && !HttpContext.RequestAborted.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    try
                    {
                        using var doc  = JsonDocument.Parse(line);
                        var root       = doc.RootElement;
                        var status     = root.TryGetProperty("status",    out var sv) ? sv.GetString() ?? "" : "";
                        long total     = root.TryGetProperty("total",     out var tv) ? tv.GetInt64()       : 0;
                        long completed = root.TryGetProperty("completed", out var cv) ? cv.GetInt64()       : 0;

                        await EmitAsync(new
                        {
                            status,
                            total,
                            completed,
                            percent = total > 0 ? (int)(completed * 100 / total)
                                                : (status == "success" ? 100 : 0),
                            done = status == "success"
                        });

                        if (status == "success") break;
                    }
                    catch { /* skip malformed lines */ }
                }
            }
            catch (OperationCanceledException) { /* client disconnected */ }
            catch (Exception ex)
            {
                try { await EmitAsync(new { error = ex.Message }); } catch { }
            }
        }

        // ── AJAX: apply (link) model to website ───────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyModel([FromForm] string url, [FromForm] string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return Json(new { success = false, message = "اسم النموذج مطلوب" });

            var config = await GetOrCreateConfigAsync();
            config.Provider        = AIProviderType.Ollama;
            config.OllamaUrl       = string.IsNullOrWhiteSpace(url) ? "http://localhost:11434" : url.Trim();
            config.OllamaModel     = model.Trim();
            config.UpdatedAt       = DateTime.UtcNow;
            config.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _context.SaveChangesAsync();
            _cache.Remove(ConfigCacheKey);

            return Json(new { success = true, message = $"✓ تم تفعيل {model} وربطه بالموقع بنجاح" });
        }

        // ── Knowledge Base ────────────────────────────────────────────

        public async Task<IActionResult> Knowledge()
        {
            var entries = await _context.AIKnowledgeEntries
                .OrderBy(e => e.SortOrder).ThenBy(e => e.CreatedAt)
                .ToListAsync();
            return View(entries);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddKnowledge(string title, string content,
            string category, int sortOrder = 0)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                TempData["KnowledgeError"] = "العنوان والمحتوى مطلوبان.";
                return RedirectToAction(nameof(Knowledge));
            }

            _context.AIKnowledgeEntries.Add(new AIKnowledgeEntry
            {
                Title              = title.Trim(),
                Content            = content.Trim(),
                Category           = string.IsNullOrWhiteSpace(category) ? "عام" : category.Trim(),
                SortOrder          = sortOrder,
                CreatedByUserId    = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
            await _context.SaveChangesAsync();

            TempData["KnowledgeSuccess"] = "تمت إضافة المعلومة بنجاح.";
            return RedirectToAction(nameof(Knowledge));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleKnowledge(int id)
        {
            var entry = await _context.AIKnowledgeEntries.FindAsync(id);
            if (entry != null)
            {
                entry.IsActive  = !entry.IsActive;
                entry.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Knowledge));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteKnowledge(int id)
        {
            var entry = await _context.AIKnowledgeEntries.FindAsync(id);
            if (entry != null)
            {
                _context.AIKnowledgeEntries.Remove(entry);
                await _context.SaveChangesAsync();
            }
            TempData["KnowledgeSuccess"] = "تم حذف المعلومة.";
            return RedirectToAction(nameof(Knowledge));
        }

        // ── Helpers ───────────────────────────────────────────────────

        private async Task<AISystemConfig> GetOrCreateConfigAsync()
        {
            var config = await _context.AISystemConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new AISystemConfig();
                _context.AISystemConfigs.Add(config);
                await _context.SaveChangesAsync();
            }
            return config;
        }

        private static bool IsValidKey(string key, string prefix)
            => !string.IsNullOrWhiteSpace(key)
               && !key.StartsWith("YOUR_")
               && key.StartsWith(prefix);

        private static string MaskKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            return key.Length > 8
                ? key[..4] + "••••••••••••" + key[^4..]
                : "••••••••";
        }

        private static string? ResolveKeyUpdate(string? submitted, string? current)
        {
            if (submitted == null) return current;
            var val = submitted.Trim();
            if (val.Contains('•')) return current;
            return val.Length == 0 ? null : val;
        }
    }
}
