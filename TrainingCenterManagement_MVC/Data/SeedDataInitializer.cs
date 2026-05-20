using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Policy;
using System.Text.Json;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.Data
{
    /// <summary>
    /// Comprehensive Seeder: creates users, courses, lectures (15 each), enrollments,
    /// payments (installments that sum exactly to course price), attendances, exams, certificates,
    /// messages and contact examples. Idempotent where possible.
    /// Call: await new SeedDataInitializer(context, userManager).SeedAllAsync();
    /// </summary>
    public class SeedDataInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeedDataInitializer(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task SeedAllAsync()
        {
            await SeedUsersAsync();
            await SeedCoursesAsync();
            await SeedLecturesAsync();
            await LinkUsersToCoursesAsync();
            await SeedPaymentsAsync();
            await SeedPresencesAsync();
            await SeedExamsAsync();
            await SeedQuestionsAsync();
            await SeedExamQuestionsAsync();
            await SeedCertificatesAsync();
            await SeedMessagesAsync();
            await SeedContactUsAsync();
        }

       



        #region Roles + Users
        private const string DefaultPassword = "P@ssw0rd!123";
        private const string SeedShamCashAccountCode = "37d26cb36d1599f159843f49ebbb75e6";
        private record SeedUser(string FullName, string EmailPrefix, Gender Gender = Gender.Male);

   

        private async Task SeedUsersAsync()
        {
            // Admins
            var admins = new List<SeedUser>
            {
                new("Ahmad Shoriqee", "ahmadshoriqeeAdmin"),
                new("Hussien AlHussien", "hussienalhussienAdmin"),
                new("Mahmoud AlHakim", "mahmoudalhakimAdmin")
            };

            // Receptionists
            var receptions = new List<SeedUser>
            {
                new("Eslam Alyousef", "eslamalyousefReception"),
                new("Kram Noman", "kramnomanReception"),
                new("Kaled Saflo", "kaledsafloReception")
            };

            // Trainers (10)
            var trainers = new List<SeedUser>
            {
                new("Fahd alhasan","fahdalhasanTrainer",Gender.Male),   new("Ali Robinson","alirobinsonTrainer",Gender.Male),
                new("Salem ali","salemaliTrainer",Gender.Male),         new("Malek Aslan","malekaslanTrainer",Gender.Male),
                new("Fras Mohammed","frasmohammedTrainer",Gender.Male), new("Rghad Shoriqee","rghadshoriqeeTrainer",Gender.Female),
                new("Waled Raslan","waledraslanTrainer",Gender.Male),   new("Marem Haj","maremhajTrainer",Gender.Female),
                new("Hasan Hassene","hasanhasseneTrainer",Gender.Male), new("Sara Yosef","sarayosefTrainer",Gender.Female)
            };

            // Trainees (20)
            var trainees = new List<SeedUser>
            {
                new("Ahmed Khaled","AhmedKhaledTrainee",Gender.Male),  new("Sara Mahmoud","SaraMahmoudTrainee",Gender.Female),
                new("Omar Hassan","OmarHassanTrainee",Gender.Male),    new("Layla Fathi","LaylaFathiTrainee",Gender.Female),
                new("Youssef Nabil","YoussefNabilTrainee",Gender.Male),new("Fatima Adel","FatimaAdelTrainee",Gender.Female),
                new("Khalid Mostafa","KhalidMostafaTrainee",Gender.Male),new("Amina Tarek","AminaTarekTrainee",Gender.Female),
                new("Hassan Ali","HassanAliTrainee",Gender.Male),      new("Rana Samir","RanaSamirTrainee",Gender.Female),
                new("Tariq Zaki","TariqZakiTrainee",Gender.Male),      new("Noor Hatem","NoorHatemTrainee",Gender.Female),
                new("Bilal Saeed","BilalSaeedTrainee",Gender.Male),    new("Mariam Kamal","MariamKamalTrainee",Gender.Female),
                new("Ziad Salem","ZiadSalemTrainee",Gender.Male),      new("Dina Yasser","DinaYasserTrainee",Gender.Female),
                new("Ali Jamal","AliJamalTrainee",Gender.Male),        new("Lama Hussein","LamaHusseinTrainee",Gender.Female),
                new("Mustafa Farid","MustafaFaridTrainee",Gender.Male),new("Huda Anwar","HudaAnwarTrainee",Gender.Female)
            };


            await CreateUsersForRole(admins, RoleType.Admin, null);
            await CreateUsersForRole(receptions, RoleType.Receptionist, null);
            await CreateUsersForRole(trainers, RoleType.Trainer, "Software Engineering");
            await CreateUsersForRole(trainees, RoleType.Trainee, null);
        }

        private async Task CreateUsersForRole(List<SeedUser> seed, RoleType role, string? specialty)
        {
            var rnd = new Random(42);
            foreach (var s in seed)
            {
                var email = $"{s.EmailPrefix}@gmail.com";
                var existing = await _userManager.FindByEmailAsync(email);
                ApplicationUser user;
                if (existing == null)
                {
                    var picIndex = s.Gender == Gender.Female
                        ? 4 + rnd.Next(0, 3)
                        : 1 + rnd.Next(0, 3);
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FullName = s.FullName,
                        Role = role,
                        Gender = s.Gender,
                        ProfilePictureUrl = $"/images/profile/{picIndex}.png",
                        BirthDate = DateTime.UtcNow.AddYears(-(18 + rnd.Next(0, 20))).AddDays(rnd.Next(0, 365))
                    };
                    var res = await _userManager.CreateAsync(user, DefaultPassword);
                    if (res.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, role.ToString());
                    }
                }
                else
                {
                    user = existing;
                }

                // create profile rows if not exist
                switch (role)
                {
                    case RoleType.Admin:
                        if (!await _context.Admins.AnyAsync(x => x.UserId == user.Id))
                            _context.Admins.Add(new Admin { UserId = user.Id, User = user });
                        break;
                    case RoleType.Receptionist:
                        var receptionist = await _context.Receptionists.FirstOrDefaultAsync(x => x.UserId == user.Id);
                        if (receptionist == null)
                        {
                            _context.Receptionists.Add(new Receptionist
                            {
                                UserId = user.Id,
                                User = user,
                                ShamCashAccountCode = SeedShamCashAccountCode
                            });
                        }
                        else
                        {
                            receptionist.ShamCashAccountCode = SeedShamCashAccountCode;
                        }
                        break;
                    case RoleType.Trainer:
                        var trainer = await _context.Trainers.FirstOrDefaultAsync(x => x.UserId == user.Id);
                        if (trainer == null)
                        {
                            _context.Trainers.Add(new Trainer
                            {
                                UserId = user.Id,
                                User = user,
                                Specialty = specialty ?? "IT",
                                YearsOfExperience = 3 + rnd.Next(0, 10),
                                BusinessLink = "https://example.com",
                                ShamCashAccountCode = SeedShamCashAccountCode
                            });
                        }
                        else
                        {
                            trainer.ShamCashAccountCode = SeedShamCashAccountCode;
                        }
                        break;
                    case RoleType.Trainee:
                        var trainee = await _context.Trainees.FirstOrDefaultAsync(x => x.UserId == user.Id);
                        if (trainee == null)
                        {
                            _context.Trainees.Add(new Trainee
                            {
                                UserId = user.Id,
                                User = user,
                                TransferCode = await GenerateUniqueTransferCodeAsync()
                            });
                        }
                        else if (string.IsNullOrWhiteSpace(trainee.TransferCode))
                        {
                            trainee.TransferCode = await GenerateUniqueTransferCodeAsync();
                        }
                        break;
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string> GenerateUniqueTransferCodeAsync()
        {
            string code;
            do
            {
                code = TransferCodeGenerator.Generate();
            }
            while (await _context.Trainees.AnyAsync(t => t.TransferCode == code));

            return code;
        }
        #endregion

        #region Courses
        private async Task SeedCoursesAsync()
        {
            // Fix any existing courses that have CourseCurrency=0 (invalid) → set to USD
            var invalidCurrencyCourses = await _context.Courses
                .Where(c => (int)c.CourseCurrency == 0)
                .ToListAsync();
            if (invalidCurrencyCourses.Any())
            {
                foreach (var c in invalidCurrencyCourses)
                    c.CourseCurrency = PaymentCurrency.USD;
                await _context.SaveChangesAsync();
            }

            if (await _context.Courses.AnyAsync(c => c.CourseName != null))
                return; // already seeded

            var AdminsId = await _context.Admins.Select(c => c.AdminId).ToListAsync();
            if (!AdminsId.Any()) return;

            var courses = new List<Course>
            {
                new Course
                {
                    CourseName = "Python Basics",
                    BatchNumber = 1,
                    NumberOfLectures = 10,
                    Price = 49,
                    CourseCurrency = PaymentCurrency.USD,
                    Description = "An introductory course to Python programming.",
                    VideoUrl = "https://www.youtube.com/watch?v=f79MRyMsjrQ",
                    ThumbnailUrl = "https://img.youtube.com/vi/f79MRyMsjrQ/0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-20),
                    AdminId = AdminsId[0],
                    CreatedDate = DateTime.UtcNow
                },
                new Course
                {
                    CourseName = "Web Development",
                    BatchNumber = 2,
                    NumberOfLectures = 15,
                    Price = 79,
                    CourseCurrency = PaymentCurrency.USD,
                    Description = "A comprehensive course on web development.",
                    VideoUrl = "https://www.youtube.com/watch?v=Ke90Tje7VS0",
                    ThumbnailUrl = "https://img.youtube.com/vi/Ke90Tje7VS0/0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-15),
                    AdminId = AdminsId[1],
                    CreatedDate = DateTime.UtcNow
                },
                new Course
                {
                    CourseName = "Advanced JavaScript",
                    BatchNumber = 1,
                    NumberOfLectures = 12,
                    Price = 69,
                    CourseCurrency = PaymentCurrency.USD,
                    Description = "Deep dive into JavaScript advanced topics.",
                    VideoUrl = "https://www.youtube.com/watch?v=PkZNo7MFNFg",
                    ThumbnailUrl = "https://img.youtube.com/vi/PkZNo7MFNFg/0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-12),
                    AdminId = AdminsId[2 % AdminsId.Count],
                    CreatedDate = DateTime.UtcNow
                },
                new Course
                {
                    CourseName = "SQL and Databases",
                    BatchNumber = 1,
                    NumberOfLectures = 10,
                    Price = 59,
                    CourseCurrency = PaymentCurrency.USD,
                    Description = "Learn SQL and database design fundamentals.",
                    VideoUrl = "https://www.youtube.com/watch?v=Oe421EPjeBE",
                    ThumbnailUrl = "https://img.youtube.com/vi/Oe421EPjeBE/0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-10),
                    AdminId = AdminsId[0],
                    CreatedDate = DateTime.UtcNow
                },
                new Course
                {
                    CourseName = "Cybersecurity Essentials",
                    BatchNumber = 1,
                    NumberOfLectures = 14,
                    Price = 89,
                    CourseCurrency = PaymentCurrency.USD,
                    Description = "Fundamentals of cybersecurity for beginners.",
                    VideoUrl = "https://www.youtube.com/watch?v=kUMe1FH4CHE",
                    ThumbnailUrl = "https://img.youtube.com/vi/kUMe1FH4CHE/0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-8),
                    AdminId = AdminsId[1 % AdminsId.Count],
                    CreatedDate = DateTime.UtcNow
                },
                new Course
                {
                    CourseName = "Machine Learning Basics",
                    BatchNumber = 1,
                    NumberOfLectures = 12,
                    Price = 99,
                    CourseCurrency = PaymentCurrency.USD,
                    Description = "Introduction to Machine Learning concepts and models.",
                    VideoUrl = "https://www.youtube.com/watch?v=GwIo3gDZCVQ",
                    ThumbnailUrl = "https://img.youtube.com/vi/GwIo3gDZCVQ/0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-6),
                    AdminId = AdminsId[2 % AdminsId.Count],
                    CreatedDate = DateTime.UtcNow
                },
                new Course
                {
                    CourseName = "Docker & Kubernetes",
                    BatchNumber = 1,
                    NumberOfLectures = 13,
                    Price = 94,
                    CourseCurrency = PaymentCurrency.USD,
                    Description = "Get started with containers and orchestration.",
                    VideoUrl = "https://www.youtube.com/watch?v=VvCytJVDoyM",
                    ThumbnailUrl = "https://img.youtube.com/vi/VvCytJVDoyM/0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-4),
                    AdminId = AdminsId[0],
                    CreatedDate = DateTime.UtcNow
                },
            };

            _context.Courses.AddRange(courses);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Lectures

        private async Task SeedLecturesAsync()
        {
            if (await _context.Lectures.AnyAsync()) return;

            var lectures = new List<Lecture>();
            var now = DateTime.UtcNow;

            // =============================
            // Python Basics (15 جلسة)
            // =============================
            var pythonBasicsId = (await _context.Courses.FirstAsync(c => c.CourseName == "Python Basics")).CourseId;
            var pythonLectures = new List<(string Title, string VideoId, string Description)>
    {
        ("Install Python & IDE setup", "rfscVS0vtbw", "Guide to installing Python and setting up an IDE."),
        ("First Python Program: Hello World", "rfscVS0vtbw", "Writing your first Python program: Hello World."),
        ("Variables and Data Types", "rfscVS0vtbw", "Understanding variables and data types in Python."),
        ("Control Flow: if/else", "rfscVS0vtbw", "Using if/else statements for control flow."),
        ("Loops: for & while", "rfscVS0vtbw", "Iterating with for and while loops."),
        ("Functions and Modules", "rfscVS0vtbw", "Defining functions and importing modules."),
        ("Working with Lists & Tuples", "rfscVS0vtbw", "Introduction to lists and tuples."),
        ("Dicts & Sets", "rfscVS0vtbw", "Using dictionaries and sets in Python."),
        ("File I/O basics", "rfscVS0vtbw", "Reading from and writing to files."),
        ("Error Handling with try/except", "rfscVS0vtbw", "Handling errors using try and except."),
        ("Working with External Libraries (pip)", "rfscVS0vtbw", "Installing and using external libraries with pip."),
        ("Object‑Oriented Python: Classes", "rfscVS0vtbw", "Basics of classes and OOP in Python."),
        ("List Comprehensions & Generators", "rfscVS0vtbw", "Using list comprehensions and generators."),
        ("Working with JSON and APIs", "rfscVS0vtbw", "Parsing JSON and interacting with APIs."),
        ("Building a Mini Project: To‑Do App", "rfscVS0vtbw", "Creating a simple To-Do app project.")
    };
            for (int i = 0; i < pythonLectures.Count; i++)
            {
                var (title, videoId, description) = pythonLectures[i];
                lectures.Add(new Lecture
                {
                    CourseId = pythonBasicsId,
                    Title = title,
                    LectureDate = now.AddDays(-(20 - i)),
                    Description = description,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                });
            }

            // =============================
            // Web Development (15 جلسة)
            // =============================
            var webDevId = (await _context.Courses.FirstAsync(c => c.CourseName == "Web Development")).CourseId;
            var webDevLectures = new List<(string Title, string VideoId, string Description)>
    {
        ("Intro to HTML: Structure & Tags", "UB1O30fR-EE", "Basics of HTML structure and tags."),
        ("Styling with CSS: Selectors & Properties", "UB1O30fR-EE", "CSS selectors and properties for styling."),
        ("Box Model & Layout Techniques", "UB1O30fR-EE", "Understanding the CSS box model and layouts."),
        ("Responsive Design: Media Queries & Flexbox", "UB1O30fR-EE", "Creating responsive designs with media queries and flexbox."),
        ("JavaScript Basics: Syntax & Variables", "UB1O30fR-EE", "Basic JavaScript syntax and variable usage."),
        ("DOM Manipulation", "UB1O30fR-EE", "Manipulating the DOM using JavaScript."),
        ("Event Handling in JS", "UB1O30fR-EE", "Handling events with JavaScript."),
        ("Ajax & Fetch API", "UB1O30fR-EE", "Using Ajax and Fetch API for asynchronous requests."),
        ("Working with Forms & Validation", "UB1O30fR-EE", "Form handling and validation techniques."),
        ("CSS Grid Layout", "UB1O30fR-EE", "CSS Grid for layout design."),
        ("Animations & Transitions", "UB1O30fR-EE", "Adding animations and transitions with CSS."),
        ("Bootstrap Fundamentals", "UB1O30fR-EE", "Using Bootstrap framework basics."),
        ("Deploying to GitHub Pages", "UB1O30fR-EE", "Deploying websites using GitHub Pages."),
        ("Intro to SEO Basics", "UB1O30fR-EE", "Search Engine Optimization fundamentals."),
        ("Building a Simple Portfolio Website", "UB1O30fR-EE", "Project: Creating a personal portfolio website.")
    };
            for (int i = 0; i < webDevLectures.Count; i++)
            {
                var (title, videoId, description) = webDevLectures[i];
                lectures.Add(new Lecture
                {
                    CourseId = webDevId,
                    Title = title,
                    LectureDate = now.AddDays(-(15 - i)),
                    Description = description,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                });
            }

            // =============================
            // Advanced JavaScript (15 جلسة)
            // =============================
            var advJsId = (await _context.Courses.FirstAsync(c => c.CourseName == "Advanced JavaScript")).CourseId;
            var advJsLectures = new List<(string Title, string VideoId, string Description)>
    {
        ("Understanding Closures", "PkZNo7MFNFg", "Deep dive into closures in JavaScript."),
        ("Scopes: var, let, const", "PkZNo7MFNFg", "Understanding variable scopes."),
        ("Prototypes & Inheritance", "PkZNo7MFNFg", "JavaScript prototypes and inheritance."),
        ("ES6+: Arrow Functions & Destructuring", "PkZNo7MFNFg", "Modern JavaScript syntax features."),
        ("Modules: import/export", "PkZNo7MFNFg", "Using JavaScript modules."),
        ("Promises in Depth", "PkZNo7MFNFg", "Understanding Promises."),
        ("Async/Await Patterns", "PkZNo7MFNFg", "Asynchronous programming with async/await."),
        ("Error Handling in Async Code", "PkZNo7MFNFg", "Handling errors in async JavaScript."),
        ("Fetch API & AJAX Deep Dive", "PkZNo7MFNFg", "Working with Fetch API and AJAX."),
        ("Functional Programming in JS", "PkZNo7MFNFg", "Introduction to functional programming."),
        ("Currying & Higher‑Order Functions", "PkZNo7MFNFg", "Advanced functional programming concepts."),
        ("Performance Optimization Techniques", "PkZNo7MFNFg", "Optimizing JavaScript code performance."),
        ("Testing with Jest", "PkZNo7MFNFg", "Testing JavaScript with Jest framework."),
        ("Webpack & Bundling Basics", "PkZNo7MFNFg", "Module bundling with Webpack."),
        ("Building a Mini JS App", "PkZNo7MFNFg", "Creating a small JavaScript application.")
    };
            for (int i = 0; i < advJsLectures.Count; i++)
            {
                var (title, videoId, description) = advJsLectures[i];
                lectures.Add(new Lecture
                {
                    CourseId = advJsId,
                    Title = title,
                    LectureDate = now.AddDays(-(12 - i)),
                    Description = description,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                });
            }

            // =============================
            // SQL and Databases (15 جلسة)
            // =============================
            var sqlDbId = (await _context.Courses.FirstAsync(c => c.CourseName == "SQL and Databases")).CourseId;
            var sqlDbLectures = new List<(string Title, string VideoId, string Description)>
    {
        ("Intro to Relational Databases", "HXV3zeQKqGY", "Introduction to relational databases."),
        ("SQL SELECT & Filtering", "HXV3zeQKqGY", "Selecting and filtering data with SQL."),
        ("WHERE Clause & Operators", "HXV3zeQKqGY", "Using WHERE clause and operators."),
        ("JOINs: INNER/LEFT/RIGHT", "HXV3zeQKqGY", "Understanding SQL joins."),
        ("GROUP BY & Aggregates", "HXV3zeQKqGY", "Grouping data and aggregate functions."),
        ("Subqueries & Nested SELECT", "HXV3zeQKqGY", "Using subqueries in SQL."),
        ("INSERT, UPDATE, DELETE Commands", "HXV3zeQKqGY", "Data modification commands."),
        ("Database Normalization", "HXV3zeQKqGY", "Database normalization principles."),
        ("Indexes & Query Optimization", "HXV3zeQKqGY", "Optimizing queries with indexes."),
        ("Transactions & ACID Properties", "HXV3zeQKqGY", "Transactions and ACID concepts."),
        ("Views and Stored Procedures", "HXV3zeQKqGY", "Using views and stored procedures."),
        ("Backup & Restore Strategies", "HXV3zeQKqGY", "Database backup and restore."),
        ("Database Security Basics", "HXV3zeQKqGY", "Fundamentals of database security."),
        ("ER Diagrams & Schema Design", "HXV3zeQKqGY", "Entity-Relationship diagrams and schema design."),
        ("Mini Project: Building a Simple Database", "HXV3zeQKqGY", "Building a simple database project.")
    };
            for (int i = 0; i < sqlDbLectures.Count; i++)
            {
                var (title, videoId, description) = sqlDbLectures[i];
                lectures.Add(new Lecture
                {
                    CourseId = sqlDbId,
                    Title = title,
                    LectureDate = now.AddDays(-(10 - i)),
                    Description = description,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                });
            }

            // =============================
            // Cybersecurity Essentials (15 جلسة)
            // =============================
            var cyberSecId = (await _context.Courses.FirstAsync(c => c.CourseName == "Cybersecurity Essentials")).CourseId;
            var cyberSecLectures = new List<(string Title, string VideoId, string Description)>
    {
        ("Cybersecurity Overview", "fGh8hmOeP_c", "Introduction to cybersecurity fundamentals."),
        ("Common Threats: Phishing, XSS, SQLi", "fGh8hmOeP_c", "Overview of common cyber threats."),
        ("Secure Password Storage", "fGh8hmOeP_c", "Techniques for secure password storage."),
        ("HTTPS & SSL/TLS", "fGh8hmOeP_c", "Understanding HTTPS and SSL/TLS protocols."),
        ("Authentication vs Authorization", "fGh8hmOeP_c", "Difference between authentication and authorization."),
        ("Firewall & Network Security", "fGh8hmOeP_c", "Basics of firewall and network security."),
        ("Intrusion Detection Basics", "fGh8hmOeP_c", "Intro to intrusion detection systems."),
        ("Secure Coding Practices", "fGh8hmOeP_c", "Writing secure code practices."),
        ("Threat Modeling", "fGh8hmOeP_c", "Identifying and modeling threats."),
        ("Incident Response Steps", "fGh8hmOeP_c", "Steps for incident response."),
        ("Encryption Fundamentals", "fGh8hmOeP_c", "Basics of encryption techniques."),
        ("Web App Security Testing", "fGh8hmOeP_c", "Testing web app security."),
        ("OWASP Top 10 Overview", "fGh8hmOeP_c", "Overview of OWASP Top 10 vulnerabilities."),
        ("Security Policies & Compliance", "fGh8hmOeP_c", "Understanding security policies."),
        ("Mini Project: Secure Login Flow", "fGh8hmOeP_c", "Building a secure login flow project.")
    };
            for (int i = 0; i < cyberSecLectures.Count; i++)
            {
                var (title, videoId, description) = cyberSecLectures[i];
                lectures.Add(new Lecture
                {
                    CourseId = cyberSecId,
                    Title = title,
                    LectureDate = now.AddDays(-(8 - i)),
                    Description = description,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                });
            }

            // =============================
            // Machine Learning Basics (15 جلسة)
            // =============================
            var mlBasicsId = (await _context.Courses.FirstAsync(c => c.CourseName == "Machine Learning Basics")).CourseId;
            var mlLectures = new List<(string Title, string VideoId, string Description)>
    {
        ("Intro to Machine Learning", "GwIo3gDZCVQ", "Introduction to machine learning concepts."),
        ("Supervised vs Unsupervised", "GwIo3gDZCVQ", "Supervised and unsupervised learning."),
        ("Linear Regression", "GwIo3gDZCVQ", "Understanding linear regression."),
        ("Logistic Regression", "GwIo3gDZCVQ", "Logistic regression explained."),
        ("Decision Trees", "GwIo3gDZCVQ", "Decision tree algorithms."),
        ("Model Evaluation Metrics", "GwIo3gDZCVQ", "Metrics for evaluating ML models."),
        ("Train/Test Split & Cross-Validation", "GwIo3gDZCVQ", "Splitting data and cross-validation."),
        ("Overfitting & Regularization", "GwIo3gDZCVQ", "Overfitting and regularization techniques."),
        ("Clustering with K‑means", "GwIo3gDZCVQ", "Clustering using K-means."),
        ("Intro to scikit‑learn", "GwIo3gDZCVQ", "Using scikit-learn library."),
        ("Feature Engineering Basics", "GwIo3gDZCVQ", "Basics of feature engineering."),
        ("Model Tuning with GridSearch", "GwIo3gDZCVQ", "Tuning models with GridSearch."),
        ("Pipelines in ML Workflows", "GwIo3gDZCVQ", "Using pipelines in ML."),
        ("Intro to Deep Learning Concepts", "GwIo3gDZCVQ", "Basic concepts of deep learning."),
        ("Mini Project: Train a Classifier", "GwIo3gDZCVQ", "Building a classifier project.")
    };
            for (int i = 0; i < mlLectures.Count; i++)
            {
                var (title, videoId, description) = mlLectures[i];
                lectures.Add(new Lecture
                {
                    CourseId = mlBasicsId,
                    Title = title,
                    LectureDate = now.AddDays(-(6 - i)),
                    Description = description,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                });
            }

            // =============================
            // Docker & Kubernetes (15 جلسة)
            // =============================
            var dockerK8sId = (await _context.Courses.FirstAsync(c => c.CourseName == "Docker & Kubernetes")).CourseId;
            var dockerK8sLectures = new List<(string Title, string VideoId, string Description)>
    {
        ("Containers & Docker Intro", "yFl2mCHdv24", "Introduction to Docker containers."),
        ("Building Docker Images", "yFl2mCHdv24", "How to build Docker images."),
        ("Running Containers", "yFl2mCHdv24", "Running containers in Docker."),
        ("Dockerfile Best Practices", "yFl2mCHdv24", "Best practices for Dockerfiles."),
        ("Networking in Docker", "yFl2mCHdv24", "Docker networking basics."),
        ("Persistent Storage (Volumes)", "yFl2mCHdv24", "Using volumes for storage."),
        ("Docker Compose Basics", "yFl2mCHdv24", "Introduction to Docker Compose."),
        ("Intro to Kubernetes", "X48VuDVv0do", "Kubernetes fundamentals."),
        ("Pods, Deployments & ReplicaSets", "X48VuDVv0do", "Understanding Kubernetes objects."),
        ("ConfigMaps & Secrets", "X48VuDVv0do", "Managing config and secrets."),
        ("Services & Ingress", "X48VuDVv0do", "Networking in Kubernetes."),
        ("Scaling & Rolling Updates", "X48VuDVv0do", "Scaling and updating apps."),
        ("Health Checks & Liveness Probes", "X48VuDVv0do", "Health monitoring in K8s."),
        ("Monitoring with kubectl & Logs", "X48VuDVv0do", "Monitoring Kubernetes clusters."),
        ("Mini Project: Deploy Web App on K8s", "X48VuDVv0do", "Deploying a web app on Kubernetes.")
    };
            for (int i = 0; i < dockerK8sLectures.Count; i++)
            {
                var (title, videoId, description) = dockerK8sLectures[i];
                lectures.Add(new Lecture
                {
                    CourseId = dockerK8sId,
                    Title = title,
                    LectureDate = now.AddDays(-(4 - i)),
                    Description = description,
                    ThumbnailUrl = $"https://img.youtube.com/vi/{videoId}/0.jpg",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                });
            }

            // حفظ جميع الجلسات في قاعدة البيانات
            _context.Lectures.AddRange(lectures);
            await _context.SaveChangesAsync();
        }



        #endregion
        #region TraineeTrainerCourses
        private async Task LinkUsersToCoursesAsync()
        {
            // جلب جميع المدربين والمتدربين مع المستخدمين المرتبطين
            var trainers = await _context.Trainers.Include(t => t.User).ToListAsync();
            var trainees = await _context.Trainees.Include(t => t.User).ToListAsync();
            var courses = await _context.Courses.ToListAsync();

            // دالة مساعدة لجلب CourseId حسب اسم الكورس
            Guid GetCourseId(string courseName) => courses.First(c => c.CourseName == courseName).CourseId;

            // ----------------------------------
            // ربط المدربين بالكورسات (محدد)
            // ----------------------------------

            // مدرب: Fahd alhasan => Python Basics, Web Development
            var fahd = trainers.FirstOrDefault(t => t.User.UserName == "fahdalhasanTrainer@gmail.com");
            var fahdCourses = new[] { "Python Basics", "Web Development" };
            var trainer = trainers.FirstOrDefault(t => t.User.UserName == "trainer@site.com");
            var trainerCourses = new[] { "Python Basics", "Web Development" };
            // مدرب: Ali Robinson => Advanced JavaScript
            var ali = trainers.FirstOrDefault(t => t.User.UserName == "alirobinsonTrainer@gmail.com");
            var aliCourses = new[] { "Advanced JavaScript" };

            // مدرب: Salem ali => SQL and Databases, Cybersecurity Essentials
            var salem = trainers.FirstOrDefault(t => t.User.UserName == "salemaliTrainer@gmail.com");
            var salemCourses = new[] { "SQL and Databases", "Cybersecurity Essentials" };

            // مدرب: Malek Aslan => Machine Learning Basics
            var malek = trainers.FirstOrDefault(t => t.User.UserName == "malekaslanTrainer@gmail.com");
            var malekCourses = new[] { "Machine Learning Basics" };

            // مدرب: Fras Mohammed => Docker & Kubernetes, Web Development
            var fras = trainers.FirstOrDefault(t => t.User.UserName == "frasmohammedTrainer@gmail.com");
            var frasCourses = new[] { "Docker & Kubernetes", "Web Development" };

            // مدرب: Rghad Shoriqee => Cybersecurity Essentials
            var rghad = trainers.FirstOrDefault(t => t.User.UserName == "rghadshoriqeeTrainer@gmail.com");
            var rghadCourses = new[] { "Cybersecurity Essentials" };

            // مدرب: Waled Raslan => Python Basics, Advanced JavaScript
            var waled = trainers.FirstOrDefault(t => t.User.UserName == "waledraslanTrainer@gmail.com");
            var waledCourses = new[] { "Python Basics", "Advanced JavaScript" };

            // مدرب: Marem Haj => SQL and Databases
            var marem = trainers.FirstOrDefault(t => t.User.UserName == "maremhajTrainer@gmail.com");
            var maremCourses = new[] { "SQL and Databases" };

            // مدرب: Hasan Hassene => Machine Learning Basics, Docker & Kubernetes
            var hasan = trainers.FirstOrDefault(t => t.User.UserName == "hasanhasseneTrainer@gmail.com");
            var hasanCourses = new[] { "Machine Learning Basics", "Docker & Kubernetes" };

            // مدرب: Sara Yosef => Web Development, Cybersecurity Essentials
            var sara = trainers.FirstOrDefault(t => t.User.UserName == "sarayosefTrainer@gmail.com");
            var saraCourses = new[] { "Web Development", "Cybersecurity Essentials" };


            // ----------------------------------
            // ربط المتدربين بالكورسات (محدد)
            // ----------------------------------

            var traineesCourseMap = new Dictionary<string, string[]>
    {
        { "AhmedKhaledTrainee@gmail.com", new[] { "Python Basics", "Web Development" } },
        { "SaraMahmoudTrainee@gmail.com", new[] { "Advanced JavaScript", "SQL and Databases" } },
        { "OmarHassanTrainee@gmail.com", new[] { "Cybersecurity Essentials", "Docker & Kubernetes" } },
        { "LaylaFathiTrainee@gmail.com", new[] { "Machine Learning Basics", "Python Basics" } },
        { "YoussefNabilTrainee@gmail.com", new[] { "Web Development", "Advanced JavaScript" } },
        { "FatimaAdelTrainee@gmail.com", new[] { "SQL and Databases", "Cybersecurity Essentials" } },
        { "KhalidMostafaTrainee@gmail.com", new[] { "Docker & Kubernetes", "Machine Learning Basics" } },
        { "AminaTarekTrainee@gmail.com", new[] { "Python Basics", "Advanced JavaScript" } },
        { "HassanAliTrainee@gmail.com", new[] { "Web Development", "SQL and Databases" } },
        { "RanaSamirTrainee@gmail.com", new[] { "Cybersecurity Essentials", "Machine Learning Basics" } },
        { "TariqZakiTrainee@gmail.com", new[] { "Docker & Kubernetes", "Python Basics" } },
        { "NoorHatemTraineev@gmail.com", new[] { "Advanced JavaScript", "Web Development" } },
        { "BilalSaeedTrainee@gmail.com", new[] { "SQL and Databases", "Docker & Kubernetes" } },
        { "MariamKamalTrainee@gmail.com", new[] { "Machine Learning Basics", "Python Basics" } },
        { "ZiadSalemTrainee@gmail.com", new[] { "Web Development", "Advanced JavaScript" } },
        { "DinaYasserTrainee@gmail.com", new[] { "Cybersecurity Essentials", "SQL and Databases" } },
        { "AliJamalTrainee@gmail.com", new[] { "Docker & Kubernetes", "Machine Learning Basics" } },
        { "LamaHusseinTrainee@gmail.com", new[] { "Python Basics", "Web Development" } },
        { "MustafaFaridTrainee@gmail.com", new[] { "Advanced JavaScript", "SQL and Databases" } },
        { "HudaAnwarTrainee@gmail.com", new[] { "Cybersecurity Essentials", "Docker & Kubernetes" } },
        { "trainee@site.com", new[] { "Cybersecurity Essentials", "Docker & Kubernetes" }}
    };


            // ----------------------------------
            // تحضير البيانات للربط في DB
            // ----------------------------------

            var courseTrainersToAdd = new List<CourseTrainer>();
            var courseTraineesToAdd = new List<CourseTrainee>();

            void AddTrainerCourses(Trainer trainer, string[] courseNames)
            {
                if (trainer == null) return;
                foreach (var courseName in courseNames)
                {
                    var courseId = GetCourseId(courseName);
                    if (!_context.CourseTrainers.Any(ct => ct.TrainerId == trainer.TrainerId && ct.CourseId == courseId))
                    {
                        courseTrainersToAdd.Add(new CourseTrainer
                        {
                            TrainerId = trainer.TrainerId,
                            CourseId = courseId
                        });
                    }
                }
            }

            void AddTraineeCourses(Trainee trainee, string[] courseNames)
            {
                if (trainee == null) return;
                foreach (var courseName in courseNames)
                {
                    var courseId = GetCourseId(courseName);
                    if (!_context.CourseTrainees.Any(ct => ct.TraineeId == trainee.TraineeId && ct.CourseId == courseId))
                    {
                        courseTraineesToAdd.Add(new CourseTrainee
                        {
                            TraineeId = trainee.TraineeId,
                            CourseId = courseId
                        });
                    }
                }
            }

            // ربط المدربين
            AddTrainerCourses(fahd, fahdCourses);
            AddTrainerCourses(trainer, trainerCourses);
            AddTrainerCourses(ali, aliCourses);
            AddTrainerCourses(salem, salemCourses);
            AddTrainerCourses(malek, malekCourses);
            AddTrainerCourses(fras, frasCourses);
            AddTrainerCourses(rghad, rghadCourses);
            AddTrainerCourses(waled, waledCourses);
            AddTrainerCourses(marem, maremCourses);
            AddTrainerCourses(hasan, hasanCourses);
            AddTrainerCourses(sara, saraCourses);

            // ربط المتدربين
            foreach (var kvp in traineesCourseMap)
            {
                var trainee = trainees.FirstOrDefault(t => t.User.UserName == kvp.Key);
                AddTraineeCourses(trainee, kvp.Value);
            }

            // حفظ التغييرات
            if (courseTrainersToAdd.Any())
                _context.CourseTrainers.AddRange(courseTrainersToAdd);

            if (courseTraineesToAdd.Any())
                _context.CourseTrainees.AddRange(courseTraineesToAdd);

            await _context.SaveChangesAsync();
        }

        #endregion
        #region Payments
        private async Task SeedPaymentsAsync()
        {
            if (await _context.Payments.AnyAsync()) return;

            var trainees = await _context.Trainees.Include(t => t.User).ToListAsync();
            var courses = await _context.Courses.ToListAsync();
            var courseTrainees = await _context.CourseTrainees.ToListAsync();

            Guid GetCourseId(string courseName) =>
                courses.First(c => c.CourseName == courseName).CourseId;

            Guid GetTraineeId(string userName) {
                var email = $"{userName}@gmail.com";
               return trainees.First(t => t.User.UserName == email).TraineeId;
            }
               

            var payments = new List<Payment>
    {
        // AhmedKhaledTrainee - Python Basics (500) => دفعتين
        new Payment { CourseId = GetCourseId("Python Basics"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), TotalAmount = 250, CreatedDate = DateTime.UtcNow.AddDays(-18) },
        new Payment { CourseId = GetCourseId("Python Basics"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), TotalAmount = 250, CreatedDate = DateTime.UtcNow.AddDays(-10) },

        // AhmedKhaledTrainee - Web Development (750) => دفعتين
        new Payment { CourseId = GetCourseId("Web Development"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), TotalAmount = 400, CreatedDate = DateTime.UtcNow.AddDays(-14) },
        new Payment { CourseId = GetCourseId("Web Development"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), TotalAmount = 350, CreatedDate = DateTime.UtcNow.AddDays(-6) },

        // SaraMahmoudTrainee - Advanced JavaScript (700) => دفعتين
        new Payment { CourseId = GetCourseId("Advanced JavaScript"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), TotalAmount = 350, CreatedDate = DateTime.UtcNow.AddDays(-11) },
        new Payment { CourseId = GetCourseId("Advanced JavaScript"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), TotalAmount = 350, CreatedDate = DateTime.UtcNow.AddDays(-4) },

        // SaraMahmoudTrainee - SQL and Databases (650) => دفعتين
        new Payment { CourseId = GetCourseId("SQL and Databases"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), TotalAmount = 325, CreatedDate = DateTime.UtcNow.AddDays(-12) },
        new Payment { CourseId = GetCourseId("SQL and Databases"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), TotalAmount = 325, CreatedDate = DateTime.UtcNow.AddDays(-3) },

        // OmarHassanTrainee - Cybersecurity Essentials (850) => دفعتين
        new Payment { CourseId = GetCourseId("Cybersecurity Essentials"), TraineeId = GetTraineeId("OmarHassanTrainee"), TotalAmount = 400, CreatedDate = DateTime.UtcNow.AddDays(-9) },
        new Payment { CourseId = GetCourseId("Cybersecurity Essentials"), TraineeId = GetTraineeId("OmarHassanTrainee"), TotalAmount = 450, CreatedDate = DateTime.UtcNow.AddDays(-2) },

        // OmarHassanTrainee - Docker & Kubernetes (900) => دفعتين
        new Payment { CourseId = GetCourseId("Docker & Kubernetes"), TraineeId = GetTraineeId("OmarHassanTrainee"), TotalAmount = 400, CreatedDate = DateTime.UtcNow.AddDays(-7) },
        new Payment { CourseId = GetCourseId("Docker & Kubernetes"), TraineeId = GetTraineeId("OmarHassanTrainee"), TotalAmount = 500, CreatedDate = DateTime.UtcNow.AddDays(-1) },

        // LaylaFathiTrainee - Machine Learning Basics (950) => دفعتين
        new Payment { CourseId = GetCourseId("Machine Learning Basics"), TraineeId = GetTraineeId("LaylaFathiTrainee"), TotalAmount = 450, CreatedDate = DateTime.UtcNow.AddDays(-13) },
        new Payment { CourseId = GetCourseId("Machine Learning Basics"), TraineeId = GetTraineeId("LaylaFathiTrainee"), TotalAmount = 500, CreatedDate = DateTime.UtcNow.AddDays(-5) },

        // LaylaFathiTrainee - Python Basics (500) => دفعة واحدة
        new Payment { CourseId = GetCourseId("Python Basics"), TraineeId = GetTraineeId("LaylaFathiTrainee"), TotalAmount = 500, CreatedDate = DateTime.UtcNow.AddDays(-8) },

        // YoussefNabilTrainee - Web Development (750) => دفعتين
        new Payment { CourseId = GetCourseId("Web Development"), TraineeId = GetTraineeId("YoussefNabilTrainee"), TotalAmount = 400, CreatedDate = DateTime.UtcNow.AddDays(-16) },
        new Payment { CourseId = GetCourseId("Web Development"), TraineeId = GetTraineeId("YoussefNabilTrainee"), TotalAmount = 350, CreatedDate = DateTime.UtcNow.AddDays(-9) },

        // YoussefNabilTrainee - Advanced JavaScript (700) => دفعة واحدة
        new Payment { CourseId = GetCourseId("Advanced JavaScript"), TraineeId = GetTraineeId("YoussefNabilTrainee"), TotalAmount = 700, CreatedDate = DateTime.UtcNow.AddDays(-6) },

        // FatimaAdelTrainee - SQL and Databases (650) => دفعتين
        new Payment { CourseId = GetCourseId("SQL and Databases"), TraineeId = GetTraineeId("FatimaAdelTrainee"), TotalAmount = 325, CreatedDate = DateTime.UtcNow.AddDays(-15) },
        new Payment { CourseId = GetCourseId("SQL and Databases"), TraineeId = GetTraineeId("FatimaAdelTrainee"), TotalAmount = 325, CreatedDate = DateTime.UtcNow.AddDays(-7) },

        // FatimaAdelTrainee - Cybersecurity Essentials (850) => دفعة واحدة
        new Payment { CourseId = GetCourseId("Cybersecurity Essentials"), TraineeId = GetTraineeId("FatimaAdelTrainee"), TotalAmount = 850, CreatedDate = DateTime.UtcNow.AddDays(-4) },

        // KhalidMostafaTrainee - Docker & Kubernetes (900) => دفعتين
        new Payment { CourseId = GetCourseId("Docker & Kubernetes"), TraineeId = GetTraineeId("KhalidMostafaTrainee"), TotalAmount = 400, CreatedDate = DateTime.UtcNow.AddDays(-12) },
        new Payment { CourseId = GetCourseId("Docker & Kubernetes"), TraineeId = GetTraineeId("KhalidMostafaTrainee"), TotalAmount = 500, CreatedDate = DateTime.UtcNow.AddDays(-3) },

        // KhalidMostafaTrainee - Machine Learning Basics (950) => دفعة واحدة
        new Payment { CourseId = GetCourseId("Machine Learning Basics"), TraineeId = GetTraineeId("KhalidMostafaTrainee"), TotalAmount = 950, CreatedDate = DateTime.UtcNow.AddDays(-1) },
    };

            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Attendance
        private async Task SeedPresencesAsync()
        {
            if (await _context.Presences.AnyAsync()) return;

            var trainees = await _context.Trainees.Include(t => t.User).ToListAsync();
            var lectures = await _context.Lectures.ToListAsync();

            Guid GetLectureId(string lectureTitle) =>
                lectures.First(l => l.Title.Trim() == lectureTitle).LectureId;

           
            Guid GetTraineeId(string userName)
            {
                var email = $"{userName}@gmail.com";
                return trainees.First(t => t.User.UserName == email).TraineeId;
            }
            var presences = new List<Presence>
    {
        // Ahmed Khaled - Python Basics
        new Presence { LectureId = GetLectureId("Install Python & IDE setup"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("First Python Program: Hello World"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Variables and Data Types"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), IsPresent = false },
        new Presence { LectureId = GetLectureId("Loops: for & while"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), IsPresent = true },

        // Ahmed Khaled - Web Development
        new Presence { LectureId = GetLectureId("Intro to HTML: Structure & Tags"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("JavaScript Basics: Syntax & Variables"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), IsPresent = false },
        new Presence { LectureId = GetLectureId("DOM Manipulation"), TraineeId = GetTraineeId("AhmedKhaledTrainee"), IsPresent = true },

        // Sara Mahmoud - Advanced JavaScript
        new Presence { LectureId = GetLectureId("Understanding Closures"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Scopes: var, let, const"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Async/Await Patterns"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), IsPresent = false },

        // Sara Mahmoud - SQL and Databases
        new Presence { LectureId = GetLectureId("Intro to Relational Databases"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("JOINs: INNER/LEFT/RIGHT"), TraineeId = GetTraineeId("SaraMahmoudTrainee"), IsPresent = true },

        // Omar Hassan - Cybersecurity Essentials
        new Presence { LectureId = GetLectureId("Cybersecurity Overview"), TraineeId = GetTraineeId("OmarHassanTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Common Threats: Phishing, XSS, SQLi"), TraineeId = GetTraineeId("OmarHassanTrainee"), IsPresent = false },
        new Presence { LectureId = GetLectureId("HTTPS & SSL/TLS"), TraineeId = GetTraineeId("OmarHassanTrainee"), IsPresent = true },

        // Omar Hassan - Docker & Kubernetes
        new Presence { LectureId = GetLectureId("Containers & Docker Intro"), TraineeId = GetTraineeId("OmarHassanTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Intro to Kubernetes"), TraineeId = GetTraineeId("OmarHassanTrainee"), IsPresent = true },

        // Layla Fathi - Machine Learning Basics
        new Presence { LectureId = GetLectureId("Intro to Machine Learning"), TraineeId = GetTraineeId("LaylaFathiTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Linear Regression"), TraineeId = GetTraineeId("LaylaFathiTrainee"), IsPresent = false },
        new Presence { LectureId = GetLectureId("Decision Trees"), TraineeId = GetTraineeId("LaylaFathiTrainee"), IsPresent = true },

        // Layla Fathi - Python Basics
        new Presence { LectureId = GetLectureId("Control Flow: if/else"), TraineeId = GetTraineeId("LaylaFathiTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Object‑Oriented Python: Classes"), TraineeId = GetTraineeId("LaylaFathiTrainee"), IsPresent = true },

        // Youssef Nabil - Web Development
        new Presence { LectureId = GetLectureId("Responsive Design: Media Queries & Flexbox"), TraineeId = GetTraineeId("YoussefNabilTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Bootstrap Fundamentals"), TraineeId = GetTraineeId("YoussefNabilTrainee"), IsPresent = false },

        // Youssef Nabil - Advanced JavaScript
        new Presence { LectureId = GetLectureId("Performance Optimization Techniques"), TraineeId = GetTraineeId("YoussefNabilTrainee"), IsPresent = true },

        // Fatima Adel - SQL and Databases
        new Presence { LectureId = GetLectureId("Transactions & ACID Properties"), TraineeId = GetTraineeId("FatimaAdelTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Database Security Basics"), TraineeId = GetTraineeId("FatimaAdelTrainee"), IsPresent = false },

        // Fatima Adel - Cybersecurity Essentials
        new Presence { LectureId = GetLectureId("OWASP Top 10 Overview"), TraineeId = GetTraineeId("FatimaAdelTrainee"), IsPresent = true },

        // Khalid Mostafa - Docker & Kubernetes
        new Presence { LectureId = GetLectureId("Dockerfile Best Practices"), TraineeId = GetTraineeId("KhalidMostafaTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Scaling & Rolling Updates"), TraineeId = GetTraineeId("KhalidMostafaTrainee"), IsPresent = true },

        // Khalid Mostafa - Machine Learning Basics
        new Presence { LectureId = GetLectureId("Overfitting & Regularization"), TraineeId = GetTraineeId("KhalidMostafaTrainee"), IsPresent = true },
        new Presence { LectureId = GetLectureId("Mini Project: Train a Classifier"), TraineeId = GetTraineeId("KhalidMostafaTrainee"), IsPresent = false },
    };

            _context.Presences.AddRange(presences);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Exams
        private async Task SeedExamsAsync()
        {
            if (await _context.Exams.AnyAsync()) return;

            var courses  = await _context.Courses.ToListAsync();
            var trainers = await _context.Trainers.Include(t => t.User).ToListAsync();

            Guid CourseId(string name) => courses.First(c => c.CourseName == name).CourseId;
            Guid TrainerId(string name) => trainers.First(t => t.User.FullName == name).TrainerId;

            // ── الامتحانات السابقة (published، منتهية) ──
            var past = new List<(string Course, string Trainer, string ExamName, DateTime Start, int Mins)>
            {
                ("Python Basics",          "Fahd alhasan",   "Python Basics - Midterm Exam",              new DateTime(2025,3,5,10,0,0),  60),
                ("Python Basics",          "Fahd alhasan",   "Python Basics - Final Exam",                new DateTime(2025,4,2,10,0,0),  90),
                ("Web Development",        "Fras Mohammed",  "Web Development - Midterm Exam",            new DateTime(2025,3,8,9,30,0),  60),
                ("Web Development",        "Fras Mohammed",  "Web Development - Final Exam",              new DateTime(2025,4,6,9,30,0),  90),
                ("Advanced JavaScript",    "Ali Robinson",   "Advanced JavaScript - Midterm Exam",        new DateTime(2025,3,10,11,0,0), 60),
                ("Advanced JavaScript",    "Ali Robinson",   "Advanced JavaScript - Final Exam",          new DateTime(2025,4,8,11,0,0),  90),
                ("SQL and Databases",      "Salem ali",      "SQL and Databases - Midterm Exam",          new DateTime(2025,3,12,14,0,0), 60),
                ("SQL and Databases",      "Salem ali",      "SQL and Databases - Final Exam",            new DateTime(2025,4,10,14,0,0), 90),
                ("Cybersecurity Essentials","Rghad Shoriqee","Cybersecurity Essentials - Midterm Exam",   new DateTime(2025,3,14,15,0,0), 60),
                ("Cybersecurity Essentials","Rghad Shoriqee","Cybersecurity Essentials - Final Exam",     new DateTime(2025,4,12,15,0,0), 90),
                ("Machine Learning Basics","Malek Aslan",    "Machine Learning Basics - Midterm Exam",    new DateTime(2025,3,16,13,0,0), 60),
                ("Machine Learning Basics","Malek Aslan",    "Machine Learning Basics - Final Exam",      new DateTime(2025,4,14,13,0,0), 90),
                ("Docker & Kubernetes",    "Hasan Hassene",  "Docker & Kubernetes - Midterm Exam",        new DateTime(2025,3,18,10,0,0), 60),
                ("Docker & Kubernetes",    "Hasan Hassene",  "Docker & Kubernetes - Final Exam",          new DateTime(2025,4,16,10,0,0), 90),
            };

            // ── امتحانات قادمة (منشورة، تبدأ في المستقبل القريب للتجربة) ──
            var now = DateTime.UtcNow;
            var upcoming = new List<(string Course, string Trainer, string ExamName, DateTime Start, int Mins)>
            {
                ("Python Basics",          "Fahd alhasan",  "Python Basics - Quiz 1",               now.AddMinutes(2),   30),
                ("Web Development",        "Fras Mohammed", "Web Development - Quiz 1",             now.AddMinutes(5),   30),
                ("Advanced JavaScript",    "Ali Robinson",  "Advanced JavaScript - Quiz 1",         now.AddHours(2),     45),
                ("SQL and Databases",      "Salem ali",     "SQL and Databases - Quiz 1",           now.AddHours(3),     45),
                ("Cybersecurity Essentials","Rghad Shoriqee","Cybersecurity Quiz - Basics",         now.AddDays(1),      60),
                ("Machine Learning Basics","Malek Aslan",   "ML Basics - Quick Assessment",         now.AddDays(2),      60),
                ("Docker & Kubernetes",    "Hasan Hassene", "Docker & Kubernetes - Pre-Final Quiz", now.AddDays(3),      45),
            };

            var exams = new List<Exam>();

            foreach (var e in past)
                exams.Add(new Exam
                {
                    CourseId        = CourseId(e.Course),
                    TrainerId       = TrainerId(e.Trainer),
                    ExamName        = e.ExamName,
                    StartDateTime   = DateTime.SpecifyKind(e.Start, DateTimeKind.Utc),
                    DurationMinutes = e.Mins,
                    PassingScore    = 60,
                    MaxAttempts     = 1,
                    IsRandomized    = true,
                    ShowResultsImmediately = true,
                    IsPublished     = true,
                    Instructions    = "اقرأ كل سؤال بعناية. لكل سؤال إجابة واحدة صحيحة فقط في أسئلة الاختيار من متعدد."
                });

            foreach (var e in upcoming)
                exams.Add(new Exam
                {
                    CourseId        = CourseId(e.Course),
                    TrainerId       = TrainerId(e.Trainer),
                    ExamName        = e.ExamName,
                    StartDateTime   = e.Start,
                    DurationMinutes = e.Mins,
                    PassingScore    = 60,
                    MaxAttempts     = 2,
                    IsRandomized    = true,
                    ShowResultsImmediately = true,
                    IsPublished     = true,
                    Instructions    = "تأكد من إكمال جميع الأسئلة قبل إرسال الامتحان. بعد الإرسال لا يمكن التعديل."
                });

            _context.Exams.AddRange(exams);
            await _context.SaveChangesAsync();
        }
        #endregion

        #region Questions & ExamQuestions
        private async Task SeedQuestionsAsync()
        {
            if (await _context.Questions.AnyAsync()) return;

            var trainers = await _context.Trainers.Include(t => t.User).ToListAsync();
            Guid TrId(string name) => trainers.First(t => t.User.FullName == name).TrainerId;

            string Opt(params string[] opts) => JsonSerializer.Serialize(opts);

            var questions = new List<Question>
            {
                // ─── Python Basics (Fahd alhasan) ───────────────────────
                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما هو الناتج الصحيح للأمر: print(type(42)) في Python؟",
                    OptionsJson=Opt("<class 'int'>","<class 'float'>","<class 'str'>","<class 'num'>"),
                    CorrectAnswer="<class 'int'>", DefaultPoints=2,
                    Explanation="القيمة 42 هي عدد صحيح integer، لذا type(42) يرجع <class 'int'>." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="أي من التالي يُستخدم لتعريف دالة في Python؟",
                    OptionsJson=Opt("function","def","fun","define"),
                    CorrectAnswer="def", DefaultPoints=2,
                    Explanation="الكلمة المحجوزة لتعريف الدوال في Python هي def." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="في Python، يمكن أن تكون القوائم (lists) متداخلة داخل بعضها.",
                    CorrectAnswer="True", DefaultPoints=1,
                    Explanation="نعم، Python تدعم القوائم المتداخلة (nested lists)." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما الفرق بين القاموس (dict) والقائمة (list) في Python؟",
                    OptionsJson=Opt("القاموس مرتب والقائمة غير مرتبة","القائمة تستخدم مفاتيح والقاموس يستخدم فهارس","القاموس يستخدم key:value أما القائمة فتستخدم فهارس رقمية","لا فرق بينهما"),
                    CorrectAnswer="القاموس يستخدم key:value أما القائمة فتستخدم فهارس رقمية", DefaultPoints=3,
                    Explanation="القاموس dict يخزن البيانات كـ key:value بينما القائمة list تستخدم فهارس رقمية تبدأ من 0." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما هو ناتج: len([1, 2, [3, 4], 5]) في Python؟",
                    OptionsJson=Opt("3","4","5","خطأ في الكود"),
                    CorrectAnswer="4", DefaultPoints=2,
                    Explanation="القائمة تحتوي على 4 عناصر: 1، 2، [3,4] (عنصر واحد هو قائمة)، 5." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="دالة range(5) في Python تُنشئ الأرقام: 0, 1, 2, 3, 4, 5.",
                    CorrectAnswer="False", DefaultPoints=1,
                    Explanation="range(5) تُنشئ: 0, 1, 2, 3, 4 (5 أرقام تبدأ من 0 حتى 4)، لا تشمل 5." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما هو الكلمة المحجوزة في Python لإيقاف الحلقة التكرارية فوراً؟",
                    CorrectAnswer="break", DefaultPoints=2,
                    Explanation="الكلمة break تُوقف تنفيذ الحلقة فوراً والانتقال إلى الكود بعدها." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="ما الناتج الصحيح للكود التالي؟\nx = [1,2,3]\ny = x\ny.append(4)\nprint(x)",
                    OptionsJson=Opt("[1, 2, 3]","[1, 2, 3, 4]","خطأ","[4, 1, 2, 3]"),
                    CorrectAnswer="[1, 2, 3, 4]", DefaultPoints=4,
                    Explanation="y = x لا تُنشئ نسخة جديدة، بل تُشير إلى نفس القائمة، لذا تعديل y يُعدّل x أيضاً." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="اكتب طريقة واحدة لعكس قائمة في Python.",
                    CorrectAnswer="reverse", DefaultPoints=3,
                    Explanation="يمكن استخدام list.reverse() أو list[::-1] أو reversed()." },

                new Question { TrainerId=TrId("Fahd alhasan"), QuestionType=QuestionType.Essay, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="اشرح الفرق بين المتغيرات المحلية (local) والعامة (global) في Python مع مثال برمجي.",
                    DefaultPoints=10,
                    Explanation="المتغير المحلي يُعرَّف داخل الدالة ولا يمكن الوصول إليه من خارجها، أما العام فيُعرَّف خارج الدوال ويمكن الوصول إليه من أي مكان." },

                // ─── Advanced JavaScript (Ali Robinson) ──────────────────
                new Question { TrainerId=TrId("Ali Robinson"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما الفرق الأساسي بين var و let في JavaScript؟",
                    OptionsJson=Opt("لا فرق بينهما","var له نطاق كتلة (block scope) وlet له نطاق دالة","let له نطاق كتلة (block scope) وvar له نطاق دالة أو عالمي","let لا يمكن إعادة تعيينه"),
                    CorrectAnswer="let له نطاق كتلة (block scope) وvar له نطاق دالة أو عالمي", DefaultPoints=3,
                    Explanation="var له function scope أو global scope، بينما let له block scope (محدود بالأقواس {})." },

                new Question { TrainerId=TrId("Ali Robinson"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="Promise في JavaScript يمكن أن يكون في حالة 'pending' فقط مرة واحدة.",
                    CorrectAnswer="True", DefaultPoints=1,
                    Explanation="بمجرد حل أو رفض الـ Promise، تنتهي حالة pending ولا يمكن العودة إليها." },

                new Question { TrainerId=TrId("Ali Robinson"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما هو ناتج: console.log(typeof null) في JavaScript؟",
                    OptionsJson=Opt("'null'","'undefined'","'object'","'boolean'"),
                    CorrectAnswer="'object'", DefaultPoints=3,
                    Explanation="هذا خطأ تاريخي مشهور في JavaScript؛ typeof null يُرجع 'object' وليس 'null'." },

                new Question { TrainerId=TrId("Ali Robinson"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="ما مفهوم Closure في JavaScript؟",
                    OptionsJson=Opt("دالة لا تُرجع قيمة","دالة تحتفظ بالوصول إلى متغيرات النطاق الخارجي حتى بعد انتهائه","طريقة لإغلاق النافذة في المتصفح","نوع خاص من الـ Promise"),
                    CorrectAnswer="دالة تحتفظ بالوصول إلى متغيرات النطاق الخارجي حتى بعد انتهائه", DefaultPoints=4,
                    Explanation="الـ Closure هو دالة تحتفظ بمرجع للمتغيرات الموجودة في النطاق الذي عُرِّفت فيه." },

                new Question { TrainerId=TrId("Ali Robinson"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما هو الأمر المستخدم للانتظار داخل دالة async في JavaScript؟",
                    CorrectAnswer="await", DefaultPoints=2,
                    Explanation="الكلمة await تُوقف تنفيذ الدالة async حتى يُحلّ الـ Promise." },

                new Question { TrainerId=TrId("Ali Robinson"), QuestionType=QuestionType.Essay, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="اشرح كيف يعمل Event Loop في JavaScript وكيف يتعامل مع المهام غير المتزامنة.",
                    DefaultPoints=10,
                    Explanation="Event Loop يراقب Call Stack وCallback Queue، وعندما يفرغ الـ Stack يأخذ المهام من الـ Queue وينفذها." },

                // ─── SQL and Databases (Salem ali) ──────────────────────
                new Question { TrainerId=TrId("Salem ali"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما وظيفة أمر SELECT في SQL؟",
                    OptionsJson=Opt("إضافة سجلات جديدة","حذف سجلات","جلب واسترجاع البيانات","تحديث البيانات الموجودة"),
                    CorrectAnswer="جلب واسترجاع البيانات", DefaultPoints=2,
                    Explanation="SELECT يُستخدم لاسترجاع البيانات من جدول أو أكثر." },

                new Question { TrainerId=TrId("Salem ali"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما الفرق بين INNER JOIN و LEFT JOIN؟",
                    OptionsJson=Opt("لا فرق بينهما","INNER JOIN يُرجع الصفوف المتطابقة فقط، LEFT JOIN يُرجع جميع صفوف الجدول الأيسر","LEFT JOIN أسرع من INNER JOIN دائماً","INNER JOIN يُرجع كل الصفوف"),
                    CorrectAnswer="INNER JOIN يُرجع الصفوف المتطابقة فقط، LEFT JOIN يُرجع جميع صفوف الجدول الأيسر", DefaultPoints=3,
                    Explanation="INNER JOIN يُرجع فقط الصفوف التي لها تطابق في كلا الجدولين. LEFT JOIN يُرجع كل صفوف الجدول الأيسر حتى لو لم يكن لها تطابق." },

                new Question { TrainerId=TrId("Salem ali"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="يمكن أن يحتوي جدول واحد في SQL على أكثر من Primary Key.",
                    CorrectAnswer="False", DefaultPoints=1,
                    Explanation="كل جدول يمكن أن يحتوي على Primary Key واحد فقط، لكنه قد يتكون من أكثر من عمود (Composite Key)." },

                new Question { TrainerId=TrId("Salem ali"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما أمر SQL المستخدم لتجميع الصفوف وحساب الإجماليات؟",
                    CorrectAnswer="GROUP BY", DefaultPoints=2,
                    Explanation="GROUP BY يُجمّع الصفوف التي لها نفس القيمة في العمود المحدد." },

                new Question { TrainerId=TrId("Salem ali"), QuestionType=QuestionType.Essay, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="اشرح مفهوم تطبيع قواعد البيانات (Database Normalization) مع توضيح أشكال 1NF و 2NF و 3NF.",
                    DefaultPoints=10,
                    Explanation="التطبيع هو عملية تنظيم البيانات في جداول لتقليل التكرار وضمان الاتساق. 1NF: لا تكرار في الأعمدة. 2NF: لا تبعية جزئية. 3NF: لا تبعية انتقالية." },

                // ─── Cybersecurity Essentials (Rghad Shoriqee) ─────────
                new Question { TrainerId=TrId("Rghad Shoriqee"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما هو هجوم Phishing؟",
                    OptionsJson=Opt("هجوم يستهدف كلمات المرور بالقوة","خداع المستخدمين للكشف عن معلوماتهم عبر رسائل مزيفة","هجوم على الشبكة لقطع الخدمة","استغلال ثغرة في قاعدة البيانات"),
                    CorrectAnswer="خداع المستخدمين للكشف عن معلوماتهم عبر رسائل مزيفة", DefaultPoints=2,
                    Explanation="Phishing هو أسلوب احتيال يخدع الضحايا للكشف عن بياناتهم الحساسة عبر رسائل بريد أو مواقع مزيفة." },

                new Question { TrainerId=TrId("Rghad Shoriqee"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="HTTPS يضمن تشفير البيانات المنقولة بين المتصفح والخادم.",
                    CorrectAnswer="True", DefaultPoints=1,
                    Explanation="HTTPS يستخدم SSL/TLS لتشفير البيانات المنقولة مما يحميها من التنصت." },

                new Question { TrainerId=TrId("Rghad Shoriqee"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما معنى SQL Injection؟",
                    OptionsJson=Opt("حقن كود SQL في نموذج إدخال لاختراق قاعدة البيانات","هجوم يمنع المستخدمين من الدخول","تشفير بيانات SQL","نسخ احتياطي لقاعدة البيانات"),
                    CorrectAnswer="حقن كود SQL في نموذج إدخال لاختراق قاعدة البيانات", DefaultPoints=3,
                    Explanation="SQL Injection هو هجوم يحقن فيه المهاجم أوامر SQL ضارة في حقول الإدخال للتحكم بقاعدة البيانات." },

                new Question { TrainerId=TrId("Rghad Shoriqee"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما المقصود بـ CIA Triad في أمن المعلومات؟",
                    CorrectAnswer="Confidentiality Integrity Availability", DefaultPoints=3,
                    Explanation="CIA Triad تمثل ثلاثة مبادئ أساسية: السرية (Confidentiality) والنزاهة (Integrity) والتوافر (Availability)." },

                new Question { TrainerId=TrId("Rghad Shoriqee"), QuestionType=QuestionType.Essay, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="اشرح هجوم Man-in-the-Middle (MITM) وكيف يمكن الحماية منه.",
                    DefaultPoints=10,
                    Explanation="MITM هو هجوم يتنصت فيه المهاجم على الاتصال بين طرفين. للحماية: استخدام HTTPS، التحقق من الشهادات الرقمية، VPN." },

                // ─── Machine Learning Basics (Malek Aslan) ──────────────
                new Question { TrainerId=TrId("Malek Aslan"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما الفرق بين Supervised و Unsupervised Learning؟",
                    OptionsJson=Opt("لا فرق بينهما","Supervised يستخدم بيانات مُصنَّفة، Unsupervised يعمل بدون تصنيف","Unsupervised أدق دائماً","Supervised لا يحتاج بيانات"),
                    CorrectAnswer="Supervised يستخدم بيانات مُصنَّفة، Unsupervised يعمل بدون تصنيف", DefaultPoints=2,
                    Explanation="Supervised Learning يتعلم من بيانات مُصنَّفة (labels)، أما Unsupervised فيبحث عن أنماط في بيانات غير مُصنَّفة." },

                new Question { TrainerId=TrId("Malek Aslan"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="Overfitting يعني أن النموذج يؤدي جيداً على بيانات التدريب لكن بشكل سيء على البيانات الجديدة.",
                    CorrectAnswer="True", DefaultPoints=1,
                    Explanation="Overfitting يحدث عندما يحفظ النموذج بيانات التدريب بدلاً من التعلم منها، فيفشل مع بيانات جديدة." },

                new Question { TrainerId=TrId("Malek Aslan"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما مهمة خوارزمية K-Means Clustering؟",
                    OptionsJson=Opt("التنبؤ بقيم مستمرة","تصنيف البيانات إلى K مجموعات بناءً على التشابه","تدريب الشبكات العصبية","قياس دقة النموذج"),
                    CorrectAnswer="تصنيف البيانات إلى K مجموعات بناءً على التشابه", DefaultPoints=3,
                    Explanation="K-Means تُقسّم البيانات إلى K مجموعة (cluster) بحيث تكون البيانات داخل كل مجموعة متشابهة." },

                new Question { TrainerId=TrId("Malek Aslan"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما مكتبة Python الأكثر شيوعاً لتحليل البيانات والمصفوفات الرقمية؟",
                    CorrectAnswer="numpy", DefaultPoints=2,
                    Explanation="NumPy هي المكتبة الأساسية للحوسبة العلمية في Python وتُوفّر دعماً للمصفوفات متعددة الأبعاد." },

                new Question { TrainerId=TrId("Malek Aslan"), QuestionType=QuestionType.Essay, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="اشرح مفهوم Train/Validation/Test Split ولماذا هو مهم في تقييم نماذج Machine Learning.",
                    DefaultPoints=10,
                    Explanation="يُقسَّم البيانات إلى 3 أجزاء: Train للتدريب، Validation لضبط المعاملات، Test للتقييم النهائي. هذا يمنع data leakage ويعطي تقييماً موضوعياً." },

                // ─── Docker & Kubernetes (Hasan Hassene) ─────────────────
                new Question { TrainerId=TrId("Hasan Hassene"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما الفرق الأساسي بين Docker Container و Virtual Machine؟",
                    OptionsJson=Opt("لا فرق بينهما","الـ Container يُشارك kernel نظام التشغيل، الـ VM لديها نظام تشغيل منفصل","الـ VM أسرع من الـ Container","الـ Container لا يمكن تشغيله على Linux"),
                    CorrectAnswer="الـ Container يُشارك kernel نظام التشغيل، الـ VM لديها نظام تشغيل منفصل", DefaultPoints=2,
                    Explanation="Container يُشارك kernel مع الـ Host OS مما يجعله أخف وأسرع، بينما VM تمتلك نظام تشغيل كامل." },

                new Question { TrainerId=TrId("Hasan Hassene"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ملف Dockerfile يُستخدم لبناء Docker Image.",
                    CorrectAnswer="True", DefaultPoints=1,
                    Explanation="Dockerfile هو ملف نصي يحتوي على تعليمات بناء الـ Image خطوة بخطوة." },

                new Question { TrainerId=TrId("Hasan Hassene"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما وظيفة Kubernetes في بيئة الـ Containers؟",
                    OptionsJson=Opt("بناء Docker Images","تشغيل Container واحد فقط","إدارة ونشر وتوسيع Containers تلقائياً","إنشاء قواعد بيانات"),
                    CorrectAnswer="إدارة ونشر وتوسيع Containers تلقائياً", DefaultPoints=3,
                    Explanation="Kubernetes هو نظام orchestration يُدير دورة حياة الـ Containers تلقائياً: النشر والتوسيع والاسترداد." },

                new Question { TrainerId=TrId("Hasan Hassene"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما أمر Docker المستخدم لتشغيل Container من Image محددة؟",
                    CorrectAnswer="docker run", DefaultPoints=2,
                    Explanation="الأمر docker run يُنشئ Container جديد ويبدأ تشغيله من الـ Image المحددة." },

                new Question { TrainerId=TrId("Hasan Hassene"), QuestionType=QuestionType.Essay, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="اشرح مفهوم Docker Compose ومتى تستخدمه مقارنةً بـ Kubernetes.",
                    DefaultPoints=10,
                    Explanation="Docker Compose لإدارة تطبيقات متعددة الـ Containers على جهاز واحد (dev environment). Kubernetes لبيئات الإنتاج والتوزيع على cluster." },

                // ─── Web Development (Fras Mohammed) ──────────────────────
                new Question { TrainerId=TrId("Fras Mohammed"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما الفرق بين HTML وCSS؟",
                    OptionsJson=Opt("HTML للتنسيق وCSS للهيكل","HTML يُبرمج التفاعل وCSS يُبرمج المنطق","HTML يُعرِّف هيكل الصفحة وCSS يُحدِّد المظهر والتنسيق","لا فرق بينهما"),
                    CorrectAnswer="HTML يُعرِّف هيكل الصفحة وCSS يُحدِّد المظهر والتنسيق", DefaultPoints=2,
                    Explanation="HTML (HyperText Markup Language) يُعرِّف محتوى وهيكل الصفحة، بينما CSS (Cascading Style Sheets) يُتحكم في التصميم والتنسيق." },

                new Question { TrainerId=TrId("Fras Mohammed"), QuestionType=QuestionType.TrueFalse, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="RESTful API يستخدم HTTP methods مثل GET وPOST وPUT وDELETE.",
                    CorrectAnswer="True", DefaultPoints=1,
                    Explanation="REST API يعتمد على HTTP methods الأساسية للتعامل مع الموارد: GET للقراءة، POST للإنشاء، PUT للتحديث، DELETE للحذف." },

                new Question { TrainerId=TrId("Fras Mohammed"), QuestionType=QuestionType.MultipleChoice, DifficultyLevel=DifficultyLevel.Medium,
                    QuestionText="ما المقصود بـ Responsive Design في تطوير الويب؟",
                    OptionsJson=Opt("تصميم يعمل على المتصفحات القديمة فقط","تصميم يتكيف مع أحجام الشاشات المختلفة تلقائياً","تصميم يُحمَّل بسرعة على الإنترنت البطيء","تصميم يدعم لغة واحدة فقط"),
                    CorrectAnswer="تصميم يتكيف مع أحجام الشاشات المختلفة تلقائياً", DefaultPoints=3,
                    Explanation="Responsive Design هو أسلوب تصميم يجعل الموقع يتكيف تلقائياً مع أحجام الشاشات المختلفة (موبايل، تابلت، سطح مكتب)." },

                new Question { TrainerId=TrId("Fras Mohammed"), QuestionType=QuestionType.ShortAnswer, DifficultyLevel=DifficultyLevel.Easy,
                    QuestionText="ما تنسيق البيانات الأكثر شيوعاً في تبادل البيانات بين الـ API والعميل؟",
                    CorrectAnswer="JSON", DefaultPoints=2,
                    Explanation="JSON (JavaScript Object Notation) هو التنسيق الأكثر شيوعاً لتبادل البيانات في الـ APIs الحديثة." },

                new Question { TrainerId=TrId("Fras Mohammed"), QuestionType=QuestionType.Essay, DifficultyLevel=DifficultyLevel.Hard,
                    QuestionText="اشرح الفرق بين Server-Side Rendering (SSR) وClient-Side Rendering (CSR) مع ذكر مزايا وعيوب كل منهما.",
                    DefaultPoints=10,
                    Explanation="SSR: يُولِّد الـ HTML في الخادم، أسرع في التحميل الأولي، أفضل لـ SEO. CSR: يُولِّد الـ HTML في المتصفح، تجربة مستخدم أفضل بعد التحميل الأولي، أبطأ في الـ SEO." },
            };

            _context.Questions.AddRange(questions);
            await _context.SaveChangesAsync();
        }

        private async Task SeedExamQuestionsAsync()
        {
            if (await _context.ExamQuestions.AnyAsync()) return;

            var exams     = await _context.Exams.ToListAsync();
            var questions = await _context.Questions.Include(q => q.Trainer).ThenInclude(t => t.User).ToListAsync();

            Guid ExamId(string name) => exams.First(e => e.ExamName == name).ExamId;

            // خريطة: اسم الامتحان → مجموعة أسئلة المدرب المُضافة
            var mappings = new Dictionary<string, (string Trainer, int Take)>
            {
                { "Python Basics - Final Exam",               ("Fahd alhasan",   9) },
                { "Advanced JavaScript - Final Exam",         ("Ali Robinson",   5) },
                { "SQL and Databases - Final Exam",           ("Salem ali",      4) },
                { "Cybersecurity Essentials - Final Exam",    ("Rghad Shoriqee", 4) },
                { "Machine Learning Basics - Final Exam",     ("Malek Aslan",    4) },
                { "Docker & Kubernetes - Final Exam",         ("Hasan Hassene",  4) },
                { "Web Development - Final Exam",             ("Fras Mohammed",  4) },
                { "Python Basics - Quiz 1",                   ("Fahd alhasan",   5) },
                { "Web Development - Quiz 1",                 ("Fras Mohammed",  3) },
                { "Advanced JavaScript - Quiz 1",             ("Ali Robinson",   4) },
                { "SQL and Databases - Quiz 1",               ("Salem ali",      3) },
            };

            var examQuestions = new List<ExamQuestion>();

            foreach (var (examName, (trainerName, take)) in mappings)
            {
                Guid examId;
                try { examId = ExamId(examName); } catch { continue; }

                var trainerQuestions = questions
                    .Where(q => q.Trainer.User.FullName == trainerName)
                    .Take(take)
                    .ToList();

                for (int i = 0; i < trainerQuestions.Count; i++)
                    examQuestions.Add(new ExamQuestion
                    {
                        ExamId     = examId,
                        QuestionId = trainerQuestions[i].QuestionId,
                        OrderIndex = i
                    });
            }

            _context.ExamQuestions.AddRange(examQuestions);
            await _context.SaveChangesAsync();
        }
        #endregion
        #region Certifcates
        private async Task SeedCertificatesAsync()
        {
            if (await _context.Certificates.AnyAsync()) return;

            var courses = await _context.Courses.ToListAsync();
            var trainees = await _context.Trainees.Include(t=>t.User).ToListAsync();
            var trainers = await _context.Trainers.Include(t => t.User).ToListAsync();
            var Exams = await _context.Exams.ToListAsync();


            Guid GetTraineeIdByName(string traineeName) =>
                                              trainees.First(c => c.User.FullName == traineeName).TraineeId;

            Guid GetTrainerIdByName(string trainerName) =>
                                              trainers.First(c => c.User.FullName == trainerName).TrainerId;
            Guid GetCourseIdByName(string courseName) =>
                                           courses.First(c => c.CourseName == courseName).CourseId;
            Guid GetExamIdByName(string examName) =>
                                           Exams.First(c => c.ExamName == examName).ExamId;
            var certificates = new List<Certificate>
                {
                    // Ahmed Khaled
                    new Certificate
                    {
                        Average = 87.5f,
                        Url = "https://example.com/certificates/AhmedKhaledTrainee_Python_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Ahmed Khaled"),
                        TrainerId = GetTrainerIdByName("Fahd alhasan"),
                        CourseId = GetCourseIdByName("Python Basics"),
                        ExamId = GetExamIdByName("Python Basics - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 90.2f,
                        Url = "https://example.com/certificates/AhmedKhaledTrainee_Web_Development.pdf",
                        TraineeId = GetTraineeIdByName("Ahmed Khaled"),
                        TrainerId = GetTrainerIdByName("Fras Mohammed"),
                        CourseId = GetCourseIdByName("Web Development"),
                        ExamId = GetExamIdByName("Web Development - Final Exam")
                    },

                    // Sara Mahmoud
                    new Certificate
                    {
                        Average = 85.0f,
                        Url = "https://example.com/certificates/SaraMahmoudTrainee_Advanced_JavaScript.pdf",
                        TraineeId = GetTraineeIdByName("Sara Mahmoud"),
                        TrainerId = GetTrainerIdByName("Ali Robinson"),
                        CourseId = GetCourseIdByName("Advanced JavaScript"),
                        ExamId = GetExamIdByName("Advanced JavaScript - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 82.4f,
                        Url = "https://example.com/certificates/SaraMahmoudTrainee_SQL_and_Databases.pdf",
                        TraineeId = GetTraineeIdByName("Sara Mahmoud"),
                        TrainerId = GetTrainerIdByName("Salem ali"),
                        CourseId = GetCourseIdByName("SQL and Databases"),
                        ExamId = GetExamIdByName("SQL and Databases - Final Exam")
                    },

                    // Omar Hassan
                    new Certificate
                    {
                        Average = 91.8f,
                        Url = "https://example.com/certificates/OmarHassanTrainee_Cybersecurity_Essentials.pdf",
                        TraineeId = GetTraineeIdByName("Omar Hassan"),
                        TrainerId = GetTrainerIdByName("Rghad Shoriqee"),
                        CourseId = GetCourseIdByName("Cybersecurity Essentials"),
                        ExamId = GetExamIdByName("Cybersecurity Essentials - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 88.6f,
                        Url = "https://example.com/certificates/OmarHassanTrainee_Docker_and_Kubernetes.pdf",
                        TraineeId = GetTraineeIdByName("Omar Hassan"),
                        TrainerId = GetTrainerIdByName("Hasan Hassene"),
                        CourseId = GetCourseIdByName("Docker & Kubernetes"),
                        ExamId = GetExamIdByName("Docker & Kubernetes - Final Exam")
                    },

                    // Layla Fathi
                    new Certificate
                    {
                        Average = 84.9f,
                        Url = "https://example.com/certificates/LaylaFathiTrainee_Machine_Learning_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Layla Fathi"),
                        TrainerId = GetTrainerIdByName("Malek Aslan"),
                        CourseId = GetCourseIdByName("Machine Learning Basics"),
                        ExamId = GetExamIdByName("Machine Learning Basics - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 89.3f,
                        Url = "https://example.com/certificates/LaylaFathiTrainee_Python_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Layla Fathi"),
                        TrainerId = GetTrainerIdByName("Fahd alhasan"),
                        CourseId = GetCourseIdByName("Python Basics"),
                        ExamId = GetExamIdByName("Python Basics - Final Exam")
                    },

                    // Youssef Nabil
                    new Certificate
                    {
                        Average = 86.7f,
                        Url = "https://example.com/certificates/YoussefNabilTrainee_Web_Development.pdf",
                        TraineeId = GetTraineeIdByName("Youssef Nabil"),
                        TrainerId = GetTrainerIdByName("Sara Yosef"),
                        CourseId = GetCourseIdByName("Web Development"),
                        ExamId = GetExamIdByName("Web Development - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 92.1f,
                        Url = "https://example.com/certificates/YoussefNabilTrainee_Advanced_JavaScript.pdf",
                        TraineeId = GetTraineeIdByName("Youssef Nabil"),
                        TrainerId = GetTrainerIdByName("Waled Raslan"),
                        CourseId = GetCourseIdByName("Advanced JavaScript"),
                        ExamId = GetExamIdByName("Advanced JavaScript - Final Exam")
                    },

                    // Fatima Adel
                    new Certificate
                    {
                        Average = 80.5f,
                        Url = "https://example.com/certificates/FatimaAdelTrainee_SQL_and_Databases.pdf",
                        TraineeId = GetTraineeIdByName("Fatima Adel"),
                        TrainerId = GetTrainerIdByName("Marem Haj"),
                        CourseId = GetCourseIdByName("SQL and Databases"),
                        ExamId = GetExamIdByName("SQL and Databases - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 94.2f,
                        Url = "https://example.com/certificates/FatimaAdelTrainee_Cybersecurity_Essentials.pdf",
                        TraineeId = GetTraineeIdByName("Fatima Adel"),
                        TrainerId = GetTrainerIdByName("Sara Yosef"),
                        CourseId = GetCourseIdByName("Cybersecurity Essentials"),
                        ExamId = GetExamIdByName("Cybersecurity Essentials - Final Exam")
                    },

                    // Khalid Mostafa
                    new Certificate
                    {
                        Average = 88.0f,
                        Url = "https://example.com/certificates/KhalidMostafaTrainee_Docker_and_Kubernetes.pdf",
                        TraineeId = GetTraineeIdByName("Khalid Mostafa"),
                        TrainerId = GetTrainerIdByName("Fras Mohammed"),
                        CourseId = GetCourseIdByName("Docker & Kubernetes"),
                        ExamId = GetExamIdByName("Docker & Kubernetes - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 83.7f,
                        Url = "https://example.com/certificates/KhalidMostafaTrainee_Machine_Learning_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Khalid Mostafa"),
                        TrainerId = GetTrainerIdByName("Hasan Hassene"),
                        CourseId = GetCourseIdByName("Machine Learning Basics"),
                        ExamId = GetExamIdByName("Machine Learning Basics - Final Exam")
                    },

                    // Amina Tarek
                    new Certificate
                    {
                        Average = 85.6f,
                        Url = "https://example.com/certificates/AminaTarekTrainee_Python_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Amina Tarek"),
                        TrainerId = GetTrainerIdByName("Waled Raslan"),
                        CourseId = GetCourseIdByName("Python Basics"),
                        ExamId = GetExamIdByName("Python Basics - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 91.3f,
                        Url = "https://example.com/certificates/AminaTarekTrainee_Advanced_JavaScript.pdf",
                        TraineeId = GetTraineeIdByName("Amina Tarek"),
                        TrainerId = GetTrainerIdByName("Ali Robinson"),
                        CourseId = GetCourseIdByName("Advanced JavaScript"),
                        ExamId = GetExamIdByName("Advanced JavaScript - Final Exam")
                    },

                    // Hassan Ali
                    new Certificate
                    {
                        Average = 88.5f,
                        Url = "https://example.com/certificates/HassanAliTrainee_Web_Development.pdf",
                        TraineeId = GetTraineeIdByName("Hassan Ali"),
                        TrainerId = GetTrainerIdByName("Sara Yosef"),
                        CourseId = GetCourseIdByName("Web Development"),
                        ExamId = GetExamIdByName("Web Development - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 84.7f,
                        Url = "https://example.com/certificates/HassanAliTrainee_SQL_and_Databases.pdf",
                        TraineeId = GetTraineeIdByName("Hassan Ali"),
                        TrainerId = GetTrainerIdByName("Salem ali"),
                        CourseId = GetCourseIdByName("SQL and Databases"),
                        ExamId = GetExamIdByName("SQL and Databases - Final Exam")
                    },

                    // Rana Samir
                    new Certificate
                    {
                        Average = 90.4f,
                        Url = "https://example.com/certificates/RanaSamirTrainee_Cybersecurity_Essentials.pdf",
                        TraineeId = GetTraineeIdByName("Rana Samir"),
                        TrainerId = GetTrainerIdByName("Rghad Shoriqee"),
                        CourseId = GetCourseIdByName("Cybersecurity Essentials"),
                        ExamId = GetExamIdByName("Cybersecurity Essentials - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 86.9f,
                        Url = "https://example.com/certificates/RanaSamirTrainee_Machine_Learning_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Rana Samir"),
                        TrainerId = GetTrainerIdByName("Hasan Hassene"),
                        CourseId = GetCourseIdByName("Machine Learning Basics"),
                        ExamId = GetExamIdByName("Machine Learning Basics - Final Exam")
                    },

                    // Tariq Zaki
                    new Certificate
                    {
                        Average = 82.0f,
                        Url = "https://example.com/certificates/TariqZakiTrainee_Docker_and_Kubernetes.pdf",
                        TraineeId = GetTraineeIdByName("Tariq Zaki"),
                        TrainerId = GetTrainerIdByName("Fras Mohammed"),
                        CourseId = GetCourseIdByName("Docker & Kubernetes"),
                        ExamId = GetExamIdByName("Docker & Kubernetes - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 89.4f,
                        Url = "https://example.com/certificates/TariqZakiTrainee_Python_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Tariq Zaki"),
                        TrainerId = GetTrainerIdByName("Fahd alhasan"),
                        CourseId = GetCourseIdByName("Python Basics"),
                        ExamId = GetExamIdByName("Python Basics - Final Exam")
                    },

                    // Noor Hatem
                    new Certificate
                    {
                        Average = 93.0f,
                        Url = "https://example.com/certificates/NoorHatemTrainee_Advanced_JavaScript.pdf",
                        TraineeId = GetTraineeIdByName("Noor Hatem"),
                        TrainerId = GetTrainerIdByName("Ali Robinson"),
                        CourseId = GetCourseIdByName("Advanced JavaScript"),
                        ExamId = GetExamIdByName("Advanced JavaScript - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 85.2f,
                        Url = "https://example.com/certificates/NoorHatemTrainee_Web_Development.pdf",
                        TraineeId = GetTraineeIdByName("Noor Hatem"),
                        TrainerId = GetTrainerIdByName("Sara Yosef"),
                        CourseId = GetCourseIdByName("Web Development"),
                        ExamId = GetExamIdByName("Web Development - Final Exam")
                    },

                    // Bilal Saeed
                    new Certificate
                    {
                        Average = 87.9f,
                        Url = "https://example.com/certificates/BilalSaeedTrainee_SQL_and_Databases.pdf",
                        TraineeId = GetTraineeIdByName("Bilal Saeed"),
                        TrainerId = GetTrainerIdByName("Marem Haj"),
                        CourseId = GetCourseIdByName("SQL and Databases"),
                        ExamId = GetExamIdByName("SQL and Databases - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 90.8f,
                        Url = "https://example.com/certificates/BilalSaeedTrainee_Docker_and_Kubernetes.pdf",
                        TraineeId = GetTraineeIdByName("Bilal Saeed"),
                        TrainerId = GetTrainerIdByName("Hasan Hassene"),
                        CourseId = GetCourseIdByName("Docker & Kubernetes"),
                        ExamId = GetExamIdByName("Docker & Kubernetes - Final Exam")
                    },

                    // Mariam Kamal
                    new Certificate
                    {
                        Average = 88.4f,
                        Url = "https://example.com/certificates/MariamKamalTrainee_Machine_Learning_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Mariam Kamal"),
                        TrainerId = GetTrainerIdByName("Malek Aslan"),
                        CourseId = GetCourseIdByName("Machine Learning Basics"),
                        ExamId = GetExamIdByName("Machine Learning Basics - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 92.6f,
                        Url = "https://example.com/certificates/MariamKamalTrainee_Python_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Mariam Kamal"),
                        TrainerId = GetTrainerIdByName("Waled Raslan"),
                        CourseId = GetCourseIdByName("Python Basics"),
                        ExamId = GetExamIdByName("Python Basics - Final Exam")
                    },

                    // Ziad Salem
                    new Certificate
                    {
                        Average = 90.0f,
                        Url = "https://example.com/certificates/ZiadSalemTrainee_Web_Development.pdf",
                        TraineeId = GetTraineeIdByName("Ziad Salem"),
                        TrainerId = GetTrainerIdByName("Fras Mohammed"),
                        CourseId = GetCourseIdByName("Web Development"),
                        ExamId = GetExamIdByName("Web Development - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 83.5f,
                        Url = "https://example.com/certificates/ZiadSalemTrainee_Advanced_JavaScript.pdf",
                        TraineeId = GetTraineeIdByName("Ziad Salem"),
                        TrainerId = GetTrainerIdByName("Waled Raslan"),
                        CourseId = GetCourseIdByName("Advanced JavaScript"),
                        ExamId = GetExamIdByName("Advanced JavaScript - Final Exam")
                    },

                    // Dina Yasser
                    new Certificate
                    {
                        Average = 84.2f,
                        Url = "https://example.com/certificates/DinaYasserTrainee_Cybersecurity_Essentials.pdf",
                        TraineeId = GetTraineeIdByName("Dina Yasser"),
                        TrainerId = GetTrainerIdByName("Sara Yosef"),
                        CourseId = GetCourseIdByName("Cybersecurity Essentials"),
                        ExamId = GetExamIdByName("Cybersecurity Essentials - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 89.7f,
                        Url = "https://example.com/certificates/DinaYasserTrainee_SQL_and_Databases.pdf",
                        TraineeId = GetTraineeIdByName("Dina Yasser"),
                        TrainerId = GetTrainerIdByName("Salem ali"),
                        CourseId = GetCourseIdByName("SQL and Databases"),
                        ExamId = GetExamIdByName("SQL and Databases - Final Exam")
                    },

                    // Ali Jamal
                    new Certificate
                    {
                        Average = 85.5f,
                        Url = "https://example.com/certificates/AliJamalTrainee_Docker_and_Kubernetes.pdf",
                        TraineeId = GetTraineeIdByName("Ali Jamal"),
                        TrainerId = GetTrainerIdByName("Fras Mohammed"),
                        CourseId = GetCourseIdByName("Docker & Kubernetes"),
                        ExamId = GetExamIdByName("Docker & Kubernetes - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 93.1f,
                        Url = "https://example.com/certificates/AliJamalTrainee_Machine_Learning_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Ali Jamal"),
                        TrainerId = GetTrainerIdByName("Hasan Hassene"),
                        CourseId = GetCourseIdByName("Machine Learning Basics"),
                        ExamId = GetExamIdByName("Machine Learning Basics - Final Exam")
                    },

                    // Lama Hussein
                    new Certificate
                    {
                        Average = 88.9f,
                        Url = "https://example.com/certificates/LamaHusseinTrainee_Python_Basics.pdf",
                        TraineeId = GetTraineeIdByName("Lama Hussein"),
                        TrainerId = GetTrainerIdByName("Fahd alhasan"),
                        CourseId = GetCourseIdByName("Python Basics"),
                        ExamId = GetExamIdByName("Python Basics - Final Exam")
                    },
                    new Certificate
                    {
                        Average = 91.0f,
                        Url = "https://example.com/certificates/LamaHusseinTrainee_Web_Development.pdf",
                        TraineeId = GetTraineeIdByName("Lama Hussein"),
                        TrainerId = GetTrainerIdByName("Sara Yosef"),
                        CourseId = GetCourseIdByName("Web Development"),
                        ExamId = GetExamIdByName("Web Development - Final Exam")
                    }

            };

             _context.Certificates.AddRange(certificates);
            await _context.SaveChangesAsync();
        }
        #endregion
        #region Messages
        private async Task SeedMessagesAsync()
        {
            if (await _context.GroupMessages.AnyAsync() || await _context.Messages.AnyAsync()) return;

            var courses = await _context.Courses.ToListAsync();
            foreach (var c in courses)
            {
                var trainer = await _context.CourseTrainers.Include(ct => ct.Trainer).ThenInclude(t => t.User).FirstOrDefaultAsync(x => x.CourseId == c.CourseId);
                if (trainer == null) continue;
                _context.GroupMessages.Add(new GroupMessage { CourseId = c.CourseId, SenderId = trainer.Trainer.UserId, Content = $"Welcome to {c.CourseName}!\nPlease review the syllabus.", Timestamp = c.ReleaseDate });
            }

            // sample direct messages
            var enrolls = await _context.CourseTrainees.Take(30).ToListAsync();
            foreach (var e in enrolls)
            {
                var trainer = await _context.CourseTrainers.Include(ct => ct.Trainer).ThenInclude(t => t.User).FirstOrDefaultAsync(x => x.CourseId == e.CourseId);
                var trainee = await _context.Trainees.Include(t => t.User).FirstOrDefaultAsync(x => x.TraineeId == e.TraineeId);
                if (trainer == null || trainee == null) continue;
                _context.Messages.Add(new Message { SenderId = trainee.UserId, ReceiverId = trainer.Trainer.UserId, Content = "Hi, I have a question about lecture 2.", IsRead = false, Timestamp = DateTime.UtcNow.AddDays(-3) });
                _context.Messages.Add(new Message { SenderId = trainer.Trainer.UserId, ReceiverId = trainee.UserId, Content = "Sure — post your question in the group and I'll answer.", IsRead = false, Timestamp = DateTime.UtcNow.AddDays(-3).AddMinutes(10) });
            }

            await _context.SaveChangesAsync();
        }
        #endregion

        #region ContactUs
        private async Task SeedContactUsAsync()
        {
            if (await _context.ContactUs.AnyAsync()) return;
            var c1 = new ContactUs { GuestId = "guest_001", CreatedAt = DateTime.UtcNow.AddDays(-7) };
            c1.GusetMessages.Add(new GusetMessage { SenderId = "guest_001", ReceiverId = "support", Content = "Is there a discount for multiple courses?", IsRead = false, Timestamp = DateTime.UtcNow.AddDays(-7) });
            c1.GusetMessages.Add(new GusetMessage { SenderId = "support", ReceiverId = "guest_001", Content = "Yes — contact reception for bundle pricing.", IsRead = true, Timestamp = DateTime.UtcNow.AddDays(-6) });

            var c2 = new ContactUs { GuestId = "guest_002", CreatedAt = DateTime.UtcNow.AddDays(-3) };
            c2.GusetMessages.Add(new GusetMessage { SenderId = "guest_002", ReceiverId = "support", Content = "Can I pay in installments for Cloud Computing?", IsRead = false, Timestamp = DateTime.UtcNow.AddDays(-3) });

            _context.ContactUs.AddRange(c1, c2);
            await _context.SaveChangesAsync();
        }
        #endregion
    }
}
