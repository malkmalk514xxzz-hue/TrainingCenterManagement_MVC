// namespace TrainingCenterManagement_MVC.Services
يمان حارس</span></div></div><div class="flex flex-col !rounded-xl border-gradient-custom mb-4 mx-auto p-3 bg-application-content shadow-lg space-y-2"><div class="flex justify-between items-start"><span class="font-semibold">منير محمد حمادة</span><span dir="ltr" class="text-sm font-bold text-red-600">-13 دولار أمريكي</span></div><div class="flex justify-between items-end"><span class="text-xs text-gray-500">#133840572</span><span class="text-xs text-application-login_text" dir="ltr">2026-03-02 - 16:07:20</span></div></div><div class="flex flex-col !rounded-xl border-gradient-custom mb-4 mx-auto p-3 bg-application-content shadow-lg space-y-2"><div class="flex justify-between items-start"><span class="font-semibold">هادي حمدو شارب</span><span dir="ltr" class="text-sm font-bold text-green-600">+43 USD</span></div><div class="flex justify-between items-end"><span class="text-xs text-gray-500">#133713224</span><span class="text-xs text-application-login_text" dir="ltr">2026-03-02 - 14:32:40</span></div></div></div><div></div></div></div></section><!--$--><!--/$--></main></div></div><script>requestAnimationFrame(function(){$RT=performance.now()});</script><script src="/_next/static/chunks/webpack-39b48b84c55a0fa7.js" crossorigin="" id="_R_" async=""></script><script>$RB=[];$RV=function(a){$RT=performance.now();for(var b=0;b<a.length;b+=2){var c=a[b],e=a[b+1];null!==e.parentNode&&e.parentNode.removeChild(e);var f=c.parentNode;if(f){var g=c.previousSibling,h=0;do{if(c&&8===c.nodeType){var d=c.data;if("/$"===d||"/&"===d)if(0===h)break;else h--;else"$"!==d&&"$?"!==d&&"$~"!==d&&"$!"!==d&&"&"!==d||h++}d=c.nextSibling;f.removeChild(c);c=d}while(c);for(;e.firstChild;)f.insertBefore(e.firstChild,c);g.data="$";g._reactRetry&&requestAnimationFrame(g._reactRetry)}}a.length=0};/ {
//     using System;
//     using System.Threading;
//     using System.Threading.Tasks;
//     using Microsoft.Extensions.Hosting;
//     using Microsoft.Extensions.Logging;
//     using PuppeteerSharp;

//     public class OrderMonitorService : BackgroundService
//     {
//         private readonly ILogger<OrderMonitorService> _logger;
//         // متغير ثابت لحفظ آخر JSON تم جلبة لكي تتمكن من عرضه في أي مكان لاحقاً
//         public static string LatestOrdersJson { get; private set; } = "[]";

//         public OrderMonitorService(ILogger<OrderMonitorService> logger)
//         {
//             _logger = logger;
//         }

//         protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//         {
//             _logger.LogInformation("تم تشغيل خدمة مراقبة العمليات في الخلفية بنجاح...");

//             // ستبقى هذه الحلقة تعمل طالما أن المشروع (السيرفر) شغال
//             while (!stoppingToken.IsCancellationRequested)
//             {
//                 try
//                 {
//                     _logger.LogInformation("جاري بدء فحص الجدول وجلب البيانات الحالية...");

//                     string json = await FetchTableDataAsJsonAsync();

//                     if (!string.IsNullOrEmpty(json) && json != "[]")
//                     {
//                         LatestOrdersJson = json; // تحديث الـ JSON العالمي بالأحدث
//                         _logger.LogInformation("تم تحديث البيانات بنجاح لآخر 10 عمليات.");
//                     }
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError($"حدث خطأ أثناء فحص البيانات في الخلفية: {ex.Message}");
//                 }

//                 // ⏱️ الانتظار قبل الفحص التالي (مثلاً كل 5 دقائق)
//                 // يمكنك تعديل الـ 5 إلى الدقائق التي تناسبك
//                 await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
//             }
//         }

//         private async Task<string> FetchTableDataAsJsonAsync()
//         {
//             var launchOptions = new LaunchOptions
//             {
//                 Headless = true,
//                 ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe"
//             };

//             using var browser = await Puppeteer.LaunchAsync(launchOptions);
//             using var page = await browser.NewPageAsync();
//             await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });

//               // 2. حقن الكوكيز الحقيقية المأخوذة من صورتك لتخطي الأمان
//        await page.SetCookieAsync(new CookieParam
//         {
//             Name = "authToken",
//             Value = "eyJhbGciOiJodHRwOi8vd3d3LnczLm9yZy8yMDAxLzA0L3htbGRzaWctbW9yZSNobWFjLXNoYTI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjI0OTcxOSIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6InNlc3Npb24iLCJTZXNzaW9uSWQiOiI0MzZjMzI0NC1jODc1LTRmMTUtOGFkYS00ZGM0MjA2MGU2ZjAiLCJpc3MiOiJzaGFtY2FzaGFwaSIsImF1ZCI6InRlc3QuYm9rbGEubWUifQ.B7gY83-_u-Jiwr0kI8IU4TiphwIiOUEXl06Nlo0c9CM", // استبدله بالتوكين الخاص بك
//             Domain = "shamcash.sy",
//             Path = "/"
//         }, new CookieParam
//         {
//             Name = "cw_conversation",
//             Value = "eyJhbGciOiJIUzI1NiJ9.eyJzb3VyY2VfaWQiOiI4ZDcxZWE5Mi1hMTNhLTRhMDQtYTE1ZS1kOTBhNjliNGQxOWMiLCJpbmJveF9pZCI6NjM2NTEsImV4cCI6MTc5NDczMjI1OCwiaWF0IjoxNzc5MTgwMjU4fQ.UAhzNTBDqFFn3sC_KQuMAVa-SE6DUBBPEP5U1rt8NbY", // استبدله بالتوكين الخاص بك
//             Domain = "shamcash.sy",
//             Path = "/"
//         }, new CookieParam
//         {
//             Name = "forge",
//             Value = "Ka+UfQV1T06JlOo9nQ3OETw5rebpirr26zn089FOTo4RPQTSQ863kg==.aAQEhohkVMPyJOj8", // استبدله بالتوكين الخاص بك
//             Domain = "shamcash.sy",
//             Path = "/"
//         }, new CookieParam
//         {
//             Name = "accessToken",
//             Value = "790a6e1fe59595167e78934e28b13d47", // استبدله بالـ Access Token الخاص بك
//             Domain = "shamcash.sy",
//             Path = "/"
//         }, new CookieParam
//         {
//             Name = "NEXT_LOCALE",
//             Value = "ar",
//             Domain = "shamcash.sy",
//             Path = "/"
//         });

//         // 3. الانتقال للرابط الصافي (تأكد من كتابته بدون فراغات أو رموز زائدة)
//         string targetUrl = "https://shamcash.sy/ar/application/transaction"; 

//             await page.GoToAsync(targetUrl, WaitUntilNavigation.Networkidle2);

//             await Task.Delay(3000); // انتظار فك التشفير وعرض المحتوى

//             // استخراج أسطر الجدول الحقيقي وتحويلها لـ JSON
//             string json = await page.EvaluateFunctionAsync<string>(@"() => {
//             const rows = document.querySelectorAll('table tbody tr');
//             const data = [];
//             const maxRows = Math.min(rows.length, 10);
            
//             for (let i = 0; i < maxRows; i++) {
//                 const cells = rows[i].querySelectorAll('td');
//                 if (cells.length >= 5) {
//                     data.push({
//                         'اسم المستخدم': cells[0] ? cells[0].innerText.trim().replace(/\n/g, ' ') : '',
//                         'رقم العملية': cells[1] ? cells[1].innerText.trim() : '',
//                         'تاريخ التسجيل': cells[2] ? cells[2].innerText.trim() : '',
//                         'المبلغ': cells[3] ? cells[3].innerText.trim() : '',
//                         'الملاحظات': cells[4] ? cells[4].innerText.trim() : ''
//                     });
//                 }
//             }
//             return JSON.stringify(data, null, 2);
//         }");

//             return json;
//         }
//     }
// }
