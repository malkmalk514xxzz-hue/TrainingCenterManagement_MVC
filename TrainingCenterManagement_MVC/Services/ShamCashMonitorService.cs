using DocumentFormat.OpenXml.Vml;
using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Text.Json;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
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

        // 🟢 الإبقاء على المتصفح والصفحة كحقول دائمة على مستوى الكلاس لكي لا تنطفئ
        private IBrowser? _globalBrowser = null;
        private IPage? _globalPage = null;

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

            // ❌ عند إغلاق المشروع نهائياً، يتم إبادة المتصفح وتحرير الذاكرة
            await CloseGlobalBrowserAsync();
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

        //private async Task CloseGlobalBrowserAsync()
        //{
        //    try
        //    {
        //        if (_globalBrowser != null)
        //        {
        //            _logger.LogInformation("يتم الآن إغلاق المتصفح العالمي لتحرير الذاكرة بسبب إيقاف السيرفس...");
        //            await _globalBrowser.CloseAsync();
        //            _globalBrowser.Dispose();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError($"خطأ أثناء محاولة إغلاق المتصفح الخارجي: {ex.Message}");
        //    }
        //}

        // 🟢 هذه الدالة يتم استدعاؤها إجبارياً من نظام .NET بمجرد إطفاء السيرفس أو المشروع
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("تم استدعاء أمر إيقاف الخدمة، جاري تدمير المتصفح أولاً...");

            // استدعاء دالة إغلاق المتصفح المضمونة
            await CloseGlobalBrowserAsync();

            // استدعاء الدالة الأصلية للسيرفس لإكمال الإطفاء بأمان
            await base.StopAsync(cancellationToken);
        }

        private async Task CloseGlobalBrowserAsync()
        {
            try
            {
                if (_globalPage != null)
                {
                    _logger.LogInformation("جاري إغلاق التاب الحالي...");
                    await _globalPage.CloseAsync();
                    _globalPage = null;
                }

                if (_globalBrowser != null)
                {
                    _logger.LogInformation("جاري إغلاق المتصفح العالمي نهائياً وتقييد العمليات المرتبطة به...");

                    // إغلاق المتصفح
                    await _globalBrowser.CloseAsync();

                    // تدمير الكائن وتحرير الذاكرة تماماً من حزمة الـ Processes في الويندوز
                    _globalBrowser.Dispose();
                    _globalBrowser = null;

                    _logger.LogInformation("تم إغلاق متصفح الكروم بنجاح وتحرير الذاكرة.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ أثناء محاولة إغلاق المتصفح الخارجي: {ex.Message}");
            }
        }
        #region الدوال الثلاث المتوافقة مع جداول الداتابيز الحقيقية

        private async Task OnNegativeAmountDetectedAsync(shamCashTranslation transaction)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            string currency = transaction.currencyType == CurrencyType.USD ? "USD" : "SYP";
            string code = transaction.notes?.Trim() ?? "";

            // البحث عن المتدرب الذي تطابق ملاحظات الحوالة رمز التحويل الخاص به
            var trainee = string.IsNullOrEmpty(code) || code == "No notes" || code == "لا يوجد ملاحظات"
                ? null
                : await db.Trainees.Include(t => t.User).FirstOrDefaultAsync(t => t.TransferCode == code);

            if (trainee != null)
            {
                // 🟢 بما أنه سحب (Withdraw)، نقوم بخصم المبلغ من رصيد المتدرب الحالي مباشرة
                if (transaction.currencyType == CurrencyType.USD)
                {
                    trainee.BalanceUSD -= transaction.amountValue;
                    if (trainee.BalanceUSD < 0) trainee.BalanceUSD = 0; // حماية لكي لا يصبح الرصيد بالسالب
                }
                else
                {
                    trainee.BalanceSYP -= transaction.amountValue;
                    if (trainee.BalanceSYP < 0) trainee.BalanceSYP = 0; // حماية لكي لا يصبح الرصيد بالسالب
                }

                await db.SaveChangesAsync();

                string amountStr = transaction.currencyType == CurrencyType.USD
                    ? $"{transaction.amountValue:N2} USD"
                    : $"{transaction.amountValue:N0} ل.س";

                // إرسال إشعار للمتدرب يفيد بخصم أو صرف المبلغ من حسابه
                await SendNotificationsToUsersAsync(db,
                    new List<string> { trainee.UserId },
                    "تحديث الرصيد: تم صرف مبلغ من حسابك",
                    $"مرحباً {trainee.User?.FullName}، تم تسجيل عملية سحب/صرف من رصيدك بمبلغ {amountStr}.\n" +
                    $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                    $"💵 رصيدك الحالي (USD): {trainee.BalanceUSD}\n" +
                    $"💱 رصيدك الحالي (SYP): {trainee.BalanceSYP}\n" +
                    $"📅 التاريخ: {transaction.registrationDate}");
            }

            // إرسال تنبيه عام للمشرفين الإداريين بوجود حركة سحب من حساب شام كاش
            var adminUserIds = await db.Admins.Select(a => a.UserId).ToListAsync();
            string title = "تنبيه: تم صرف مبلغ من حساب الموقع";
            string message =
                $"تم صرف مبلغ من حساب الموقع على شام كاش.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"👤 الاسم على شام كاش: {transaction.userName}\n" +
                (trainee != null ? $"🧑‍🎓 اسم المتدرب على الموقع: {trainee.User?.FullName}\n" : "⚠️ الحوالة بدون ملاحظات واضحة لمتدرب معين.\n") +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ المصروف: {transaction.amountValue} {currency}\n" +
                $"📅 التاريخ: {transaction.registrationDate}\n" +
                $"📝 الملاحظات الواردة: {transaction.notes}\n" +
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
            string title = "تنبيه: تحويلة واردة بدون رمز تحويل";
            string message =
                $"قام أحد الأشخاص بإرسال تحويلة (إيداع) بدون ذكر رمز التحويل الخاص به في الملاحظات.\n" +
                $"يرجى مراجعة تفاصيل الحوالة يدوياً لربطها بالمتدرب المناسب.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"👤 الاسم على شام كاش: {transaction.userName}\n" +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ: {transaction.amountValue} {currency}\n" +
                $"📅 التاريخ: {transaction.registrationDate}\n" +
                $"━━━━━━━━━━━━━━━━━━━";

            await SendNotificationsToUsersAsync(db, targetUserIds, title, message);
        }

        private async Task OnCustomNotesDetectedAsync(shamCashTranslation transaction)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            string code = transaction.notes?.Trim() ?? "";

            // البحث عن المتدرب المطابق لرمز التحويل المكتوب في الملاحظات
            var trainee = await db.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TransferCode == code);

            if (trainee == null)
            {
                _logger.LogWarning($"لم يتم العثور على متدرب برمز التحويل الوارد: {code}. سيتم تحويلها كحوالة بدون ملاحظات.");
                await OnNoNotesDetectedAsync(transaction);
                return;
            }

            // 🟢 بما أنه إيداع/شحن (Deposit)، نقوم بإضافة المبلغ إلى رصيد المتدرب الحالي
            if (transaction.currencyType == CurrencyType.USD)
                trainee.BalanceUSD += transaction.amountValue;
            else
                trainee.BalanceSYP += transaction.amountValue;

            await db.SaveChangesAsync();

            // إنشاء سجلات دفع تلقائية لدورات المتدرب التي لديها رصيد متبقي
            await CreatePaymentRecordsFromShamCashAsync(db, trainee, transaction);

            var adminUserIds = await db.Admins.Select(a => a.UserId).ToListAsync();

            string currency = transaction.currencyType == CurrencyType.USD ? "USD" : "SYP";
            string traineeName = trainee.User?.FullName ?? trainee.UserId;

            string traineeTitle = "تم شحن رصيدك بنجاح 🎉";
            string adminTitle = $"تم شحن رصيد متدرب: {traineeName}";

            string traineeMessage =
                $"تم إضافة {transaction.amountValue} {currency} إلى رصيدك بنجاح.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ المضاف: {transaction.amountValue} {currency}\n" +
                $"💵 رصيدك الحالي (USD): {trainee.BalanceUSD}\n" +
                $"💱 رصيدك الحالي (SYP): {trainee.BalanceSYP}\n" +
                $"📅 التاريخ: {transaction.registrationDate}";

            string adminMessage =
                $"تم شحن رصيد متدرب آلياً عبر نظام المراقبة.\n" +
                $"━━━━━━━━━━━━━━━━━━━\n" +
                $"🧑‍🎓 المتدرب على الموقع: {traineeName}\n" +
                $"👤 مُرسِل الحوالة على شام كاش: {transaction.userName}\n" +
                $"🔢 رقم العملية: #{transaction.transactionId}\n" +
                $"💸 المبلغ المشحون: {transaction.amountValue} {currency}\n" +
                $"📅 التاريخ: {transaction.registrationDate}";

            await SendNotificationsToUsersAsync(db, new List<string> { trainee.UserId }, traineeTitle, traineeMessage);
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

        #region مستخرج البيانات المستمر الذكي (Persistent Web Scraper)
        private async Task<List<shamCashTranslation>> FetchTransactionsFromWebAsync()
        {
            var list = new List<shamCashTranslation>();

            try
            {
                string lastSavedId = await GetLastTransactionIdFromDatabaseAsync() ?? "229246375";

                string targetUrl = _configuration["ShamCashConfig:TargetUrl"] ?? "";
                string authToken = _configuration["ShamCashConfig:Cookies:AuthToken"] ?? "";
                string accessToken = _configuration["ShamCashConfig:Cookies:AccessToken"] ?? "";
                string cw_conversation = _configuration["ShamCashConfig:Cookies:cw_conversation"] ?? "";
                string forge = _configuration["ShamCashConfig:Cookies:forge"] ?? "";
                string nextLocale = _configuration["ShamCashConfig:Cookies:NextLocale"] ?? "";

                string lKey1 = _configuration["ShamCashConfig:LocalStorageKey1"] ?? "";
                string lVal1 = _configuration["ShamCashConfig:LocalStorageValue1"] ?? "";
                string lKey2 = _configuration["ShamCashConfig:LocalStorageKey2"] ?? "";
                string lVal2 = _configuration["ShamCashConfig:LocalStorageValue2"] ?? "";
                string lKey3 = _configuration["ShamCashConfig:LocalStorageKey3"] ?? "";
                string lVal3 = _configuration["ShamCashConfig:LocalStorageValue3"] ?? "";

                string sKey = _configuration["ShamCashConfig:SessionStorageKey"] ?? "";
                string sVal = _configuration["ShamCashConfig:SessionStorageValue"] ?? "";

                bool isScrapingDone = false;

                try
                {
                    // 🟢 1. إذا كان المتصفح والصفحة منشأين مسبقاً، نحاول عمل تحديث (Reload) فوراً
                    if (_globalBrowser != null && _globalPage != null)
                    {
                        _logger.LogInformation("المتصفح جاهز، جاري تحديث الصفحة الحالية...");

                        // تمرير null للـ timeout والمصفوفة كمعامل ثانٍ لمنع أي خطأ أحمر في بناء المشروع
                        await _globalPage.ReloadAsync(null, new[] { WaitUntilNavigation.Networkidle2 });
                        await Task.Delay(2000);

                        isScrapingDone = true;
                    }
                }
                catch (Exception reloadEx)
                {
                    _logger.LogWarning($"فشل تحديث الصفحة (ربما أُغلق المتصفح): {reloadEx.Message}. سيتم إعادة تشغيل متصفح جديد...");
                    _globalPage = null;
                    _globalBrowser = null;
                }

                // 🟢 2. إذا كان المتصفح غير موجود، أو أُغلق وانهار؛ نقوم بإنشاء متصفح جديد وحقن الكوكيز
                if (!isScrapingDone)
                {
                    _logger.LogInformation("جاري إعداد وتشغيل جلسة متصفح جديدة...");

                    var launchOptions = new LaunchOptions
                    {
                        Headless = true,
                        ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                        Args = new[]
                        {
                            "--no-sandbox",
                            "--disable-setuid-sandbox",
                            "--disable-dev-shm-usage"
                        }
                    };

                    _globalBrowser = await Puppeteer.LaunchAsync(launchOptions);
                    _globalPage = await _globalBrowser.NewPageAsync();

                    await _globalPage.SetRequestInterceptionAsync(true);
                    _globalPage.Request += (sender, e) =>
                    {
                        if (e.Request.ResourceType == PuppeteerSharp.ResourceType.Image ||
                            e.Request.ResourceType == PuppeteerSharp.ResourceType.Media ||
                            e.Request.ResourceType == PuppeteerSharp.ResourceType.Font)
                            e.Request.AbortAsync();
                        else
                            e.Request.ContinueAsync();
                    };

                    await _globalPage.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 800 });

                    await _globalPage.SetCookieAsync(new CookieParam
                    {
                        Name = "authToken",
                        Value = authToken,
                        Domain = "shamcash.sy",
                        Path = "/"
                    }, new CookieParam
                    {
                        Name = "accessToken",
                        Value = accessToken,
                        Domain = "shamcash.sy",
                        Path = "/"
                    }, new CookieParam
                    {
                        Name = "forge",
                        Value = forge,
                        Domain = "shamcash.sy",
                        Path = "/"
                    }, new CookieParam
                    {
                        Name = "cw_conversation",
                        Value = cw_conversation,
                        Domain = "shamcash.sy",
                        Path = "/"
                    }, new CookieParam
                    {
                        Name = "NEXT_LOCALE",
                        Value = nextLocale,
                        Domain = "shamcash.sy",
                        Path = "/"
                    });

                    await _globalPage.GoToAsync(targetUrl, WaitUntilNavigation.Networkidle2);

                    await _globalPage.EvaluateFunctionAsync(@"(lk1, lv1, lk2, lv2, lk3, lv3, sk, sv) => {
                        if (lk1 && lv1) localStorage.setItem(lk1, lv1);
                        if (lk2 && lv2) localStorage.setItem(lk2, lv2);
                        if (lk3 && lv3) localStorage.setItem(lk3, lv3);
                        if (sk && sv) sessionStorage.setItem(sk, sv);
                    }", lKey1, lVal1, lKey2, lVal2, lKey3, lVal3, sKey, sVal);

                    await Task.Delay(3000);
                }

                // استخراج الجدول البرمجي باستخدام الصفحة الحية المستقرة
                string rawJson = await _globalPage!.EvaluateFunctionAsync<string>(@" (lastSavedId) => {
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
                _logger.LogError($"خطأ أثناء استخراج البيانات من المتصفح المستمر: {ex.Message}");
            }

            // تم إزالة حزمة الـ finally بالكامل لضمان استمرار عمل الكروم وعدم إغلاقه بعد القراءة
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

        #region إنشاء سجلات الدفع التلقائية من شام كاش

        private async Task CreatePaymentRecordsFromShamCashAsync(
            ApplicationDbContext db,
            Trainee trainee,
            shamCashTranslation transaction)
        {
            try
            {
                string txNote = $"[شام كاش] #{transaction.transactionId}";

                // منع التكرار: إذا كان هذا الرقم موجوداً في سجلات الدفع مسبقاً، لا نضيف مجدداً
                bool alreadyRecorded = await db.Payments
                    .AnyAsync(p => p.TraineeId == trainee.TraineeId &&
                                   p.Notes != null &&
                                   p.Notes.Contains($"#{transaction.transactionId}"));
                if (alreadyRecorded) return;

                // تحميل أسعار الصرف
                var dbRates = await db.ExchangeRates.ToListAsync();
                var rates = new Dictionary<PaymentCurrency, decimal>(CurrencyHelper.DefaultRates);
                foreach (var r in dbRates) rates[r.Currency] = r.RateToSYP;

                // تحويل مبلغ الإيداع إلى ليرة سورية
                decimal depositSYP = transaction.currencyType == CurrencyType.USD
                    ? transaction.amountValue * rates[PaymentCurrency.USD]
                    : transaction.amountValue;

                // جلب الدورات المسجل بها المتدرب مرتبةً حسب تاريخ التسجيل
                var enrollments = await db.CourseTrainees
                    .Where(ct => ct.TraineeId == trainee.TraineeId)
                    .Include(ct => ct.Course)
                    .Where(ct => ct.Course != null && !ct.Course.IsDeleted)
                    .OrderBy(ct => ct.EnrolledAt)
                    .ToListAsync();

                decimal remainingToApply = depositSYP;

                foreach (var enrollment in enrollments)
                {
                    if (remainingToApply <= 0) break;

                    var existingPayments = await db.Payments
                        .Where(p => p.TraineeId == trainee.TraineeId &&
                                    p.CourseId == enrollment.CourseId &&
                                    !p.IsDeleted)
                        .ToListAsync();

                    decimal paidSYP = existingPayments.Sum(p =>
                        CurrencyHelper.ToSYP(p.TotalAmount, p.Currency, rates));
                    decimal courseRemaining = enrollment.Course.Price - paidSYP;

                    if (courseRemaining <= 0) continue;

                    decimal payThisCourse = Math.Min(remainingToApply, courseRemaining);

                    db.Payments.Add(new Payment
                    {
                        PaymentId   = Guid.NewGuid(),
                        TraineeId   = trainee.TraineeId,
                        CourseId    = enrollment.CourseId,
                        TotalAmount = payThisCourse,
                        Currency    = PaymentCurrency.SYP,
                        Notes       = $"{txNote} | {transaction.userName}",
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted   = false
                    });

                    remainingToApply -= payThisCourse;
                }

                if (depositSYP > remainingToApply)
                    await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ أثناء إنشاء سجلات الدفع التلقائية من شام كاش: {ex.Message}");
            }
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