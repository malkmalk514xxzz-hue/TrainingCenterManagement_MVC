عند انشاء حساب متدرب بتم انشاء حساب لاله يتم انشاء رمز بشكل تلقائي ارقام و احرف صغيرة و كبيرة اسمه رمز التحويل و هو خاص بكل طالب له رمز خاص به

اما المدرب و الReceptionist هدول عند انشاء الحساب يطلب منهم نص اسمه كود حساب الشام كاش يكون على هذه النمطيه 1906b1ad48773bc8785b9b2004d3ccc6

و قم بتعديل الملف SeedDataInitializer ليتم جعل للمتدربين الي تم انشاءهم مسبقا رمز تحويل و اجعل للمدربين و لل Receptionist كود شام كاش للكل متل بعضهم لانهم بيانات وهمية يكون هاد الكود 37d26cb36d1599f159843f49ebbb75e6 للجميع الي تم انشاءهم بملف ال SeedDataInitializer 

بملف ال ShamCashMonitorService يوجد 3 دوال فاضية بدي يتم تعبئتها بالتالي 

OnNegativeAmountDetectedAsync : هاي الدالة عند استدعائها بتم ارسال اشعار للمدير "تم صرف مبلغ من حساب الموقع " و بعدها بتم وصع بيانات الحةالة التي تمت بتم تصميمهن بكارد طبعا البيانات بكني من الكلاس 
shamCashTranslation

OnNoNotesDetectedAsync بتم ارسال اشعار للمدير و لل Receptionist انو هنالك متدرب قام بارسال تحويلة بدون كود التحويل الخاص به قم برد المال له كمان بتم عرض البيانات الخاصة بهالتحويلة 

OnCustomNotesDetectedAsync هون بتم اخذ الكود الموجود بالبيانات التحويلة بالمتحول notes و مقارنته مع جميع الاكواد الي عملناها للمتدربين المتدرب الي الكود الي بالرسالة مطابق لكوده مناخذ البيانات و منزيد رصيده بالمبلغ المرسل مع مراعات اذا كان SYP او USD طبعا يجيب اضافة لكل طالب خانة اسمها رصيد وحدة USD و اخرى SYP و اذا ال notes ما طلعت متل اي طالب بتم استدعات التابع OnNoNotesDetectedAsync

الطالب يبين شقد معه بالسوري و بالدولار و حدهن رمز التحويل الخاص فيه
 لا تضع الشام كاش لتسجيل الدخول فقط بانشاء حساب


---
بدي تعديل واجة الداشبورد للطالب ليصير يظهر عنده شقد معه بالموقع سوري و دولار و كود الي بحول عليه و حدها كبسه ايداع كبسة الايداع باتخذه لشاشة فيها صورة باركود هاد "wwwroot\images\static\shamCashAccountForWebSite.png" 

و بيعرصله بقاه انسخ كود تحويلك و ضعه كملاحظه الى هذا الباركود و بيعرضله هاي الصورة "wwwroot\images\static\howTransfier.png" الي ماشر فيها وين يحط كود التحويل 

و اعمل اي طالي بيقدر يشوف كل الدورات و بيجي عنده خيار تسجيل و سعر الدورة على اله مو مسجل فيهن بس كبس تسحيل بتظهرله شاشة تعريفية عن الكورس و فيها زر دفع و حده شقد سعرها بس كبس دفع بتم مقارنة سعر الدورة بالسوري مع محصلة شقد معه بين (دولار ضرب 130 + سوري )اذا معه اكثر بينشرا الكورس و بينخصم منه اذا اقل بتطلعله شاشة بتقله اعتذار و شقد بده سوري اسا فوق مصرياته و بتجيه شاشة قم بايداع المبلغ اذا ضغط عليها بتاخذه على شاشة الي خطينا فيها باركود الدفع 

بس انتهى من الدفع بتم ادراج اسمه مع الكورس ز بصير مسجل فيه يعني بيقدر يحضر كل محاضراته و هيك 


---
تحويل العملة يصير من خلال ال ApiForTrans الي بال appsetting 
رده يكون على هذا الشكل 
{"usdsypd":{"symbol":"usdsypd","value":13960,"sell":13960,"buy":13900,"last_close":13910,"price_updated_at":"2026-05-20T08:34:05.000000Z"}}
خذ قيمى الموضوعة في value و قسمها على 100 تكون هذه قيمة اليرة السورية مقابل الدولار الامريكي 


عندما يقوم المتدرب بالشحن يجب ان ياتي الاشعار 
"تم شحن رصيد متدرب" ثم اسم المستخدم في موقعنا الموتبط بكود التحويل 

اريد وضع زر اسمه استرداد الاموال للاموال الباقية بالحساب اما الي اشترى فيها كورس راحت عند ما بيكبس استرداد بتاخذه على صفحة فيها طلب لصورة باركود حسابه و ادخل المبلغ المراد سحبه 
لازم المبلغ اقل او يساوي مصراته طبعا هون اذا معه سوري لا يسحب الاسوري و اذا معه دولر كذلك الامر يعني مافي تحويل عملات و بس ضغط استرداد بتم خصمهن مباشر من حسابه و وضعهن بوضع المراجعة تحت الي معه بلون برتقالي  و بيجي اشعار للمدير و لل 
Receptionist و بس المدير او ال Receptionist كبس تم بيظهرله باركود الشخص و كود تحويله و بصير يشوف ال Withdraw من حساب الموقع بشوف كود التحويل اذا نفس كود التحويل الخاص بالمتدرب بيخصم من المبلغ الي صار بالمراجعة بقدر ما خرج من الحساب وكل ما بتم تحويل لهالشخص مبلغ بيجيه اشعار تمت الموافقة على المبلغ كذا 


---

طلع معي هالخطا 
بصفحة المدرب بالداشبورد
An unhandled exception occurred while processing the request.
ArgumentOutOfRangeException: Specified time is not supported in this calendar. It should be between 04/30/1900 00:00:00 (Gregorian date) and 11/16/2077 23:59:59 (Gregorian date), inclusive. (Parameter 'time')
Actual value was 456604038890000000.
System.Globalization.UmAlQuraCalendar.CheckTicksRange(long ticks)


و هالخطا عند طلب الاسترداد 

An unhandled exception occurred while processing the request.
SqlException: Invalid object name 'WithdrawRequests'.
Microsoft.Data.SqlClient.SqlConnection.OnError(SqlException exception, bool breakConnection, Action<Action> wrapCloseInAction)


---

طلعلي هالخطا بصفحة الاسترداد

An unhandled exception occurred while processing the request.
SqlException: Invalid object name 'WithdrawRequests'.
Microsoft.Data.SqlClient.SqlConnection.OnError(SqlException exception, bool breakConnection, Action<Action> wrapCloseInAction)

DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
Microsoft.EntityFrameworkCore.Update.AffectedCountModificationCommandBatch.ConsumeAsync(RelationalDataReader reader, CancellationToken cancellationToken)


-----

عدل شاشة الداشبورد للمتدرب بحيث 



----

في صفحة ال /Payments & /PaymentManagement & /PaymentRequests/Manage & /Settings/Edit & /ExchangeRates & /Salary ان السايد بار على الطرف اليسار اريده ان يذهب الى الطرف اليمين 

و صفحة ال /LiveSessions & /Certificates & /PaymentManagement & /PaymentRequests/Manage & /Settings/Edit & /ExchangeRates  & /Salary & /AdminAI/Settings  يصبح الوان السايد بار الى الازرق متل الباقي هي اخضر يصير ازرق 

و اجعل الواجهة /Courses تاخذ ايضا سايد بار مشابه 
يعني السايد بار لازم يصير مثل الي بهاي الصفحة بكل الصفحا /Dashboard/AdminDashboard

و في السايد بار ازل هذا الزر من صفحة المدير فقط فقط فقط /Exams/QuestionBank زر بنك الاسئلة

و في الدفع عن طريق الشام كاش التي هي بالكلاس ShamCashMonitorService.cs و طريقة الدفع التي هي عند ال /Payments/Create  
اريد عند الدفع بشام كاش او بتيك الطريقة يتم زيادة المبلغ الذي هو في الصفحة /Payments و تزيد علميات الدفع 


----

عند الانتقال من الصفحة الرئيسية الى الداشبورد عم يطول كتير افحص لماذا و ماذا يفعل بالترتيب من ضغط الزر لعرض الداشبورد افحص فقط لا تعدل شي فقط افحص و صفحة عرض الامتخانات ايضا عم تطول 

اشو رايك اذا عملناهن asinc بيتغير شي ؟

طبعا متل ما قلتلك لا تعدل فقط افحص و اقترح حل و اعرض اشو عم يعمل خطوة خطوة 

----


في واجهة تاكيد اي شهادة /Certificates/Verify/[id] زر الطباعة اجعله /Certificates/CertificatePdf/[id]بدل ما يستدعي شاشة الطباعة

----

واجهة عرض و تعديل الشهادة هاي العرض /Certificates/Details/[id] وهاي التعديل /Certificates/Edit/[id] و انشاء شهادة كمان /Certificates/Create  عدل شكلهن ليصير بتصميم الموقع تبعنا و يكون احترافي شيءا ما

----

بالنسبة للتحويل من شام كاش انا عندي لما بيدفع على حساب مربوط بالموقع ما بده لا تغيير ولا شي هو مربوط و ما بدنا نغيره سالفة اعدادات الدفع الالكتروني شيلها اخوي 
انا قلتلك بدي لما يدفع اي لما احد الدوال ال 3 في السيرفس ShamCashMonitorService.cs مبيتات كل وحدة اشو شغلتها و متعامل معهن نظامي لكن لما بيدفع و بحط الملاحظة المتفق عليها بدي اياه وببتم التتعرف على مين دفع و شقد دفع بدي القيمة تضاف للذمم المالية نظامي و هيك ويعني بزيد رصيد هذا الطالب بالمبلغ الي دفعه و في عند الطالب شالداشبورد بنل فيها شقد دافع بالشام كاش بدي ينحط فيها شقد دافع بالشام كاش تحتهن اذا دافع بغير طريقى يعني عن طريق الادارة ينحط تختهن 


--------------


اعدادات الدفع و طلبات الدفع شيلهن كلهن 
و الايداع عن طريق شام كاش عن طريق /Trainees/Deposit جاهزة 
و الدفع عم يحفظ بقاعدة الببيانات بملف ما طل الدفات الي عم تصير عن طريق الشام كاش اما كل شي ضفته اله علاقة بباينس ارجع شيله رجعه متل ما كان تماما 
و بالنسبة لعرض شفد الشخص عامل ايداع بالشام كاش انا قديما عامل قايمة بتظهر ليش انت رجعت حطيت غيرها ارجع شيلها الي انت حطيتها اما اذا الشخص دافع عن طريق 

عند الضغط على استرداد بشاشة الطالب و بعد ما يدخل معلومات الطلب عند الضغط على طلب عم يطلع غلط 
An unhandled exception occurred while processing the request.
SqlException: Invalid object name 'WithdrawRequests'.
Microsoft.Data.SqlClient.SqlConnection.OnError(SqlException exception, bool breakConnection, Action<Action> wrapCloseInAction)

DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
Microsoft.EntityFrameworkCore.Update.AffectedCountModificationCommandBatch.ConsumeAsync(RelationalDataReader reader, CancellationToken cancellationToken)

------

اسا في الفيو /PaymentRequests/MyRequests ما غيرت الدفع و هيك 
شيل باينس و الشام كاش حط رابط الايداع تبع الشام كاش /Trainees/Deposit

----

في السايد بار الخاص ب /Courses & /ExchangeRates
لون الشعار Training Center اخضر  اعمله ازرق متل باقي الدوال 