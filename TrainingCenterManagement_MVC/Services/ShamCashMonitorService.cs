using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using PuppeteerSharp;
using TrainingCenterManagement_MVC.Data;
using Messaging_Chat_Application_MahmoudHakim.Hubs;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
    public class ShamCashMonitorService : BackgroundService
    {
        private readonly ILogger<ShamCashMonitorService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ChatHub> _hubContext;
        private string _lastProcessedTransactionId = null;

        public ShamCashMonitorService(
            ILogger<ShamCashMonitorService> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            IHubContext<ChatHub> hubContext)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("تم تشغيل خدمة مراقبة ShamCash في الخلفية بنجاح...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _lastProcessedTransactionId = await GetLastTransactionIdFromDatabaseAsync();

                    if (string.IsNullOrEmpty(_lastProcessedTransactionId))
                    {
                        _logger.LogInformation("قاعدة البيانات فارغة حالياً. سيتم الاعتماد على الحوالة الافتراضية كحد أدنى: 229246375");
                        _lastProcessedTransactionId = "229246375";
                    }

                    _logger.LogInformation($"جاري فحص الموقع والمقارنة مع آخر تحويلة مسجلة لدينا: #{_lastProcessedTransactionId}");

                    List<shamCashTranslation> currentTransactions = await FetchTransactionsFromWebAsync();

                    if (currentTransactions != null && currentTransactions.Count > 0)
                    {
                        currentTransactions.Reverse();

                        bool hasNewTransactions = false;

                        foreach (var tx in currentTransactions)
                        {
                            if (IsNewerTransaction(tx.transactionId, _lastProcessedTransactionId))
                            {
                                hasNewTransactions = true;

                                string txInfo = $"[حوالة جديدة] اسم المستخدم: {tx.userName} | رقم العملية: #{tx.transactionId} | المبلغ: {tx.amountValue} | دفع ام استلام {tx.transactionType} |  نوع العملة {tx.currencyType} | التاريخ: {tx.registrationDate} | الملاحظات: {tx.notes}";
                                _logger.LogInformation(txInfo);
                                Console.WriteLine(txInfo);

                                await ProcessTransactionFiltersAsync(tx);
                                await SaveTransactionToDatabaseAsync(tx);

                                _lastProcessedTransactionId = tx.transactionId;
                            }
                        }

                        if (!hasNewTransactions)
                        {
                            string noUpdateMsg = "لا يوجد أي تحويلات جديدة في الموقع حتى الآن.";
                            _logger.LogInformation(noUpdateMsg);
                            Console.WriteLine(noUpdateMsg);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("لم يتم العثور على أسطر بيانات داخل الجدول أو فشل الاتصال بالموقع.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"حدث خطأ أثناء دورة الفحص في الخلفية: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task ProcessTransactionFiltersAsync(shamCashTranslation tx)
        {
            if (tx.transactionType == TransactionType.Withdraw)
            {
                await OnNegativeAmountDetectedAsync(tx);
                return;
            }

            string cleanNotes = tx.notes?.Trim() ?? "";

            if (cleanNotes == "No notes" || cleanNotes == "لا يوجد ملاحظات" || string.IsNullOrEmpty(cleanNotes))
            {
                await OnNoNotesDetectedAsync(tx);
            }
            else
            {
                await OnCustomNotesDetectedAsync(tx);
            }
        }

        #region الدوال الثلاث

        private async Task OnNegativeAmountDetectedAsync(shamCashTranslation transaction)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            string currency = transaction.currencyType == CurrencyType.USD ? "USD" : "SYP";

            // Check if the notes match a trainee's transfer code → this is an admin refund payment
            string notes = transaction.notes?.Trim() ?? "";
            var trainee = string.IsNullOrEmpty(notes) ? null
                : await db.Trainees.Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.TransferCode == notes);

            if (trainee != null)
            {
                // Match pending withdraw request for this trainee
                var pendingRequest = await db.WithdrawRequests
                    .Where(w => w.TraineeId == trainee.TraineeId && w.Status != WithdrawStatus.FullyApproved)
                    .OrderBy(w => w.CreatedAt)
                    .FirstOrDefaultAsync();

                if (pendingRequest != null)
                {
                    if (transaction.currencyType == CurrencyType.USD)
                    {
                        pendingRequest.PaidAmountUSD += transaction.amountValue;
                        if (pendingRequest.PaidAmountUSD >= pendingRequest.AmountUSD &&
                            pendingRequest.PaidAmountSYP >= pendingRequest.AmountSYP)
                            pendingRequest.Status = WithdrawStatus.FullyApproved;
                        else
                            pendingRequest.Status = WithdrawStatus.PartiallyApproved;
                    }
                    else
                    {
                        pendingRequest.PaidAmountSYP += transaction.amountValue;
                        if (pendingRequest.PaidAmountUSD >= pendingRequest.AmountUSD &&
                            pendingRequest.PaidAmountSYP >= pendingRequest.AmountSYP)
                            pendingRequest.Status = WithdrawStatus.FullyApproved;
                        else
                            pendingRequest.Status = WithdrawStatus.PartiallyApproved;
                    }
                    await db.SaveChangesAsync();

                    string approvedStr = transaction.currencyType == CurrencyType.USD
                        ? $"{transaction.amountValue:N2} USD"
                        : $"{transaction.amountValue:N0} ل.س";

                    await SendNotificationsToUsersAsync(db,
                        [trainee.UserId],
                        "تمت الموافقة على طلب استردادك",
                        $"تمت الموافقة على مبلغ {approvedStr} من طلب الاسترداد الخاص بك.\n" +
                        $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                        $"📅 التاريخ: {transaction.registrationDate}");
                }
            }

            // Notify admins regardless
            var adminUserIds = await db.Admins.Select(a => a.UserId).ToListAsync();
            string title = "تنبيه: تم صرف مبلغ من حساب الموقع";
            string message =
                $"تم صرف مبلغ من حساب الموقع على شام كاش.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"👤 الاسم على شام كاش: {transaction.userName}\n" +
                (trainee != null ? $"🧑‍🎓 اسم المتدرب على الموقع: {trainee.User?.FullName}\n" : "") +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ: {transaction.amountValue} {currency}\n" +
                $"📅 التاريخ: {transaction.registrationDate}\n" +
                $"📝 الملاحظات: {transaction.notes}\n" +
                $"━━━━━━━━━━━━━━━━━━━";

            await SendNotificationsToUsersAsync(db, adminUserIds, title, message);
        }

        private async Task OnNoNotesDetectedAsync(shamCashTranslation transaction)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var adminUserIds = await db.Admins.Select(a => a.UserId).ToListAsync();
            var receptionistUserIds = await db.Receptionists.Select(r => r.UserId).ToListAsync();
            var targetUserIds = adminUserIds.Union(receptionistUserIds).Distinct().ToList();

            string currency = transaction.currencyType == CurrencyType.USD ? "USD" : "SYP";
            string title = "تنبيه: تحويلة بدون رمز تحويل";
            string message =
                $"قام أحد الأشخاص بإرسال تحويلة بدون رمز التحويل الخاص به.\n" +
                $"يرجى مراجعة التفاصيل وإعادة المبلغ للمُحوِّل.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"👤 الاسم: {transaction.userName}\n" +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ: {transaction.amountValue} {currency}\n" +
                $"📅 التاريخ: {transaction.registrationDate}\n" +
                $"📝 الملاحظات: {(string.IsNullOrWhiteSpace(transaction.notes) ? "لا توجد ملاحظات" : transaction.notes)}\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"⚠️ يرجى قم برد المبلغ للمُرسِل.";

            await SendNotificationsToUsersAsync(db, targetUserIds, title, message);
        }

        private async Task OnCustomNotesDetectedAsync(shamCashTranslation transaction)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            string code = transaction.notes?.Trim() ?? "";

            var trainee = await db.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TransferCode == code);

            if (trainee == null)
            {
                _logger.LogWarning($"لم يتم العثور على متدرب برمز التحويل: {code}");
                await OnNoNotesDetectedAsync(transaction);
                return;
            }

            if (transaction.currencyType == CurrencyType.USD)
                trainee.BalanceUSD += transaction.amountValue;
            else
                trainee.BalanceSYP += transaction.amountValue;

            await db.SaveChangesAsync();

            var adminUserIds = await db.Admins.Select(a => a.UserId).ToListAsync();

            string currency = transaction.currencyType == CurrencyType.USD ? "USD" : "SYP";
            string traineeName = trainee.User?.FullName ?? trainee.UserId;
            string traineeTitle = "تم شحن رصيدك بنجاح";
            string adminTitle = $"تم شحن رصيد متدرب {traineeName}";
            string traineeMessage =
                $"تم إضافة {transaction.amountValue} {currency} إلى رصيدك.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ المضاف: {transaction.amountValue} {currency}\n" +
                $"💵 رصيدك الحالي (USD): {trainee.BalanceUSD}\n" +
                $"💱 رصيدك الحالي (SYP): {trainee.BalanceSYP}\n" +
                $"📅 التاريخ: {transaction.registrationDate}";

            string adminMessage =
                $"تم شحن رصيد متدرب عبر شام كاش.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"🧑‍🎓 المتدرب على الموقع: {traineeName}\n" +
                $"👤 مُرسِل الحوالة على شام كاش: {transaction.userName}\n" +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ: {transaction.amountValue} {currency}\n" +
                $"📅 التاريخ: {transaction.registrationDate}";

            await SendNotificationsToUsersAsync(db, [trainee.UserId], traineeTitle, traineeMessage);
            await SendNotificationsToUsersAsync(db, adminUserIds, adminTitle, adminMessage);
        }

        #endregion

        #region مساعد الإشعارات

        private async Task SendNotificationsToUsersAsync(ApplicationDbContext db, List<string> userIds, string title, string message)
        {
            var notifications = userIds.Select(uid => new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = title,
                Message = message,
                Type = NotificationType.General,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync();

            foreach (var uid in userIds)
            {
                var connections = await db.UserConnections
                    .Where(uc => uc.UserId == uid && uc.IsConnected)
                    .Select(uc => uc.ConnectionId)
                    .ToListAsync();

                foreach (var connId in connections)
                {
                    await _hubContext.Clients.Client(connId)
                        .SendAsync("ReceiveNotification", title, message);
                }
            }
        }

        #endregion

        #region مستخرج البيانات (Web Scraper)
        private async Task<List<shamCashTranslation>> FetchTransactionsFromWebAsync()
        {
            var list = new List<shamCashTranslation>();
            IBrowser? browser = null;

            try
            {
                string lastSavedId = await GetLastTransactionIdFromDatabaseAsync() ?? "229246375";

                string targetUrl      = _configuration["ShamCashConfig:TargetUrl"] ?? "";
                string authToken      = _configuration["ShamCashConfig:Cookies:AuthToken"] ?? "";
                string accessToken    = _configuration["ShamCashConfig:Cookies:AccessToken"] ?? "";
                string cw_conversation= _configuration["ShamCashConfig:Cookies:cw_conversation"] ?? "";
                string forge          = _configuration["ShamCashConfig:Cookies:forge"] ?? "";
                string nextLocale     = _configuration["ShamCashConfig:Cookies:NextLocale"] ?? "";

                var launchOptions = new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-extensions",
                        "--disable-gpu",
                        "--blink-settings=imagesEnabled=false",
                        "--no-zygote",
                        "--single-process"
                    }
                };

                browser = await Puppeteer.LaunchAsync(launchOptions);
                using var page = await browser.NewPageAsync();

                await page.SetRequestInterceptionAsync(true);
                page.Request += (sender, e) =>
                {
                    if (e.Request.ResourceType == PuppeteerSharp.ResourceType.Image ||
                        e.Request.ResourceType == PuppeteerSharp.ResourceType.Media ||
                        e.Request.ResourceType == PuppeteerSharp.ResourceType.Font)
                        e.Request.AbortAsync();
                    else
                        e.Request.ContinueAsync();
                };

                await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 800 });

                await page.SetCookieAsync(new CookieParam
                {
                    Name = "authToken", Value = authToken, Domain = "shamcash.sy", Path = "/"
                }, new CookieParam
                {
                    Name = "accessToken", Value = accessToken, Domain = "shamcash.sy", Path = "/"
                }, new CookieParam
                {
                    Name = "forge", Value = forge, Domain = "shamcash.sy", Path = "/"
                }, new CookieParam
                {
                    Name = "cw_conversation", Value = cw_conversation, Domain = "shamcash.sy", Path = "/"
                }, new CookieParam
                {
                    Name = "NEXT_LOCALE", Value = nextLocale, Domain = "shamcash.sy", Path = "/"
                });

                await page.GoToAsync(targetUrl, WaitUntilNavigation.Networkidle2);
                await Task.Delay(3000);

                string rawJson = await page.EvaluateFunctionAsync<string>(@" (lastSavedId) => {
            const rows = document.querySelectorAll('table tbody tr');
            const data = [];

            for (let i = 0; i < rows.length; i++) {
                const cells = rows[i].querySelectorAll('td');
                if (cells.length >= 5) {
                    const currentId = cells[1] ? cells[1].innerText.trim().replace('#', '') : '';

                    if (currentId === lastSavedId) {
                        break;
                    }

                    data.push({
                        userName: cells[0] ? cells[0].innerText.trim().replace(/\n/g, ' ') : '',
                        transactionId: currentId,
                        registrationDate: cells[2] ? cells[2].innerText.trim() : '',
                        rawAmount: cells[3] ? cells[3].innerText.trim() : '',
                        notes: cells[4] ? cells[4].innerText.trim() : ''
                    });
                }
            }
            return JSON.stringify(data);
        }", lastSavedId);

                if (!string.IsNullOrEmpty(rawJson))
                {
                    var rawList = JsonSerializer.Deserialize<List<RawTransactionDto>>(rawJson) ?? [];

                    foreach (var raw in rawList)
                    {
                        var tx = new shamCashTranslation
                        {
                            userName = raw.userName,
                            transactionId = raw.transactionId,
                            registrationDate = raw.registrationDate,
                            notes = raw.notes
                        };

                        string amountText = raw.rawAmount ?? "";

                        if (amountText.Contains("-"))
                            tx.transactionType = TransactionType.Withdraw;
                        else
                            tx.transactionType = TransactionType.Deposit;

                        if (amountText.ToLower().Contains("usd"))
                            tx.currencyType = CurrencyType.USD;
                        else if (amountText.ToLower().Contains("syp") || amountText.Contains("ل.س"))
                            tx.currencyType = CurrencyType.SYP;
                        else
                            tx.currencyType = CurrencyType.Unknown;

                        string cleanNumber = amountText.Substring(1, amountText.Length - 4);
                        if (decimal.TryParse(cleanNumber, out decimal parsedValue))
                            tx.amountValue = parsedValue;
                        else
                            tx.amountValue = 0;

                        list.Add(tx);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ أثناء استخراج جدول المتصفح: {ex.Message}");
            }
            finally
            {
                if (browser != null)
                {
                    await browser.CloseAsync();
                    browser.Dispose();
                }
            }

            return list;
        }
        #endregion

        #region منطق الـ Entity Framework وقاعدة البيانات

        private async Task<string> GetLastTransactionIdFromDatabaseAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var lastRow = db.shamCashTranslation
                            .OrderByDescending(x => x.id)
                            .FirstOrDefault();

            return lastRow?.transactionId;
        }

        private async Task SaveTransactionToDatabaseAsync(shamCashTranslation tx)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.shamCashTranslation.Add(tx);
            await db.SaveChangesAsync();
        }

        private bool IsNewerTransaction(string currentId, string lastProcessedId)
        {
            if (long.TryParse(currentId, out long current) && long.TryParse(lastProcessedId, out long last))
                return current > last;

            return currentId != lastProcessedId;
        }

        #endregion
    }

    public class RawTransactionDto
    {
        public string userName { get; set; }
        public string transactionId { get; set; }
        public string registrationDate { get; set; }
        public string rawAmount { get; set; }
        public string notes { get; set; }
    }
}
