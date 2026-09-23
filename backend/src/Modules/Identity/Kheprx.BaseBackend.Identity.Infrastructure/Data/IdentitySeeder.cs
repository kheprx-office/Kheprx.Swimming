using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Data;

public static class IdentitySeeder
{
    // Dev credentials (documented in docs/STARTER.md — not production secrets).
    public const string DevPassword = "Passw0rd!";

    public static async Task SeedAsync(IdentityDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        var headCoachRole = await EnsureRole(db, "head_coach", "Head Coach", "المدرب العام", ct);
        var captainRole = await EnsureRole(db, "captain", "Captain", "الكابتن", ct);
        var swimmerRole = await EnsureRole(db, "swimmer", "Swimmer", "السبّاح", ct);
        var male = await EnsureGender(db, "male", "Male", "ذكر", ct);
        var female = await EnsureGender(db, "female", "Female", "أنثى", ct);
        await db.SaveChangesAsync(ct);

        await EnsureStrokes(db, ct);
        await EnsureBloodTypes(db, ct);
        await EnsureFitnessAssessments(db, ct);
        await EnsureGuardianRelations(db, ct);
        await EnsureObservationCategories(db, ct);
        await EnsureFeedbackCategories(db, ct);
        await EnsureAttendanceStatuses(db, ct);
        await EnsureClubs(db, ct);
        await db.SaveChangesAsync(ct);

        await EnsureHeadCoach(db, hasher, headCoachRole.Id, male.Id, ct);
        await EnsureCaptain(db, hasher, captainRole.Id, male.Id, ct);
        await db.SaveChangesAsync(ct);

        await EnsureSwimmers(db, hasher, swimmerRole.Id, male.Id, female.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Role> EnsureRole(IdentityDbContext db, string code, string en, string ar, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (role is null) { role = new Role(code, en, ar); await db.Roles.AddAsync(role, ct); }
        return role;
    }

    private static async Task<Gender> EnsureGender(IdentityDbContext db, string code, string en, string ar, CancellationToken ct)
    {
        var gender = await db.Genders.FirstOrDefaultAsync(g => g.Code == code, ct);
        if (gender is null) { gender = new Gender(code, en, ar); await db.Genders.AddAsync(gender, ct); }
        return gender;
    }

    private static async Task EnsureHeadCoach(IdentityDbContext db, IPasswordHasher hasher, Guid roleId, Guid genderId, CancellationToken ct)
    {
        const string email = "headcoach@kheprx.local";
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;
        var user = new AppUser("head.coach", "Head Coach", roleId, nameAr: "المدرب العام",
            email: email, passwordHash: hasher.Hash(DevPassword), genderId: genderId,
            dob: new DateOnly(1985, 3, 12), phone: "+201000000001", isFirstLogin: true);
        await db.Users.AddAsync(user, ct);
        await db.HeadCoachProfiles.AddAsync(new HeadCoachProfile(user.Id, "28503121234567"), ct);
    }

    private static async Task EnsureCaptain(IdentityDbContext db, IPasswordHasher hasher, Guid roleId, Guid genderId, CancellationToken ct)
    {
        const string email = "captain@kheprx.local";
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;
        var user = new AppUser("captain.dave", "Captain Dave", roleId, nameAr: "الكابتن ديف",
            email: email, passwordHash: hasher.Hash(DevPassword), genderId: genderId,
            dob: new DateOnly(1990, 7, 5), phone: "+201000000002", isFirstLogin: false);
        await db.Users.AddAsync(user, ct);
        await db.CaptainProfiles.AddAsync(new CaptainProfile(user.Id, "29007051234567"), ct);
    }

    private static async Task EnsureSwimmers(IdentityDbContext db, IPasswordHasher hasher,
        Guid swimmerRoleId, Guid maleId, Guid femaleId, CancellationToken ct)
    {
        if (await db.SwimmerProfiles.AnyAsync(ct)) return; // idempotent — seed only when empty

        var clubId = await db.Clubs.OrderBy(c => c.NameEn).Select(c => c.Id).FirstAsync(ct);
        var strokeId = await db.Strokes.OrderBy(s => s.NameEn).Select(s => s.Id).FirstAsync(ct);
        var passwordHash = hasher.Hash(DevPassword);

        for (var i = 1; i <= 24; i++)
        {
            var user = new AppUser(
                username: $"swimmer{i:D2}", nameEn: $"Swimmer {i:D2}", roleId: swimmerRoleId,
                nameAr: $"سبّاح {i:D2}", passwordHash: passwordHash,
                genderId: i % 2 == 0 ? femaleId : maleId,
                dob: new DateOnly(2010, 1, 1).AddDays(i), isFirstLogin: true);
            await db.Users.AddAsync(user, ct);

            var profile = new SwimmerProfile(user.Id, $"SW-{i:D4}", clubId);
            await db.SwimmerProfiles.AddAsync(profile, ct);
            await db.SwimmerSpecializations.AddAsync(new SwimmerSpecialization(profile.Id, strokeId), ct);
        }
    }

    private static async Task EnsureStrokes(IdentityDbContext db, CancellationToken ct)
    {
        await EnsureStroke(db, "freestyle", "Freestyle", "حرة", ct);
        await EnsureStroke(db, "backstroke", "Backstroke", "ظهر", ct);
        await EnsureStroke(db, "butterfly", "Butterfly", "فراشة", ct);
        await EnsureStroke(db, "breaststroke", "Breaststroke", "صدر", ct);
        await EnsureStroke(db, "medley", "IM", "متنوع فردي", ct);
    }

    private static async Task EnsureStroke(IdentityDbContext db, string code, string en, string ar, CancellationToken ct)
    {
        if (!await db.Strokes.AnyAsync(s => s.Code == code, ct))
            await db.Strokes.AddAsync(new Stroke(code, en, ar), ct);
    }

    private static async Task EnsureBloodTypes(IdentityDbContext db, CancellationToken ct)
    {
        foreach (var code in new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" })
            if (!await db.BloodTypes.AnyAsync(b => b.Code == code, ct))
                await db.BloodTypes.AddAsync(new BloodType(code, code, code), ct);
    }

    private static async Task EnsureFitnessAssessments(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("fit", "Fit", "لائق"),
            ("unfit", "Unfit", "غير لائق"),
            ("under_review", "Under Review", "قيد المراجعة"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.FitnessAssessments.AnyAsync(f => f.Code == code, ct))
                await db.FitnessAssessments.AddAsync(new FitnessAssessment(code, en, ar), ct);
    }

    private static async Task EnsureGuardianRelations(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("father", "Father", "الأب"),
            ("mother", "Mother", "الأم"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.GuardianRelations.AnyAsync(r => r.Code == code, ct))
                await db.GuardianRelations.AddAsync(new GuardianRelation(code, en, ar), ct);
    }

    private static async Task EnsureObservationCategories(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("allergy", "Allergy", "حساسية"),
            ("surgery", "Surgery", "جراحة"),
            ("chronic", "Chronic", "مرض مزمن"),
            ("autoimmune", "Autoimmune", "مناعي ذاتي"),
            ("composition", "Composition", "تركيب الجسم"),
            ("flag", "Flag", "ملاحظة"),
            ("other", "Other", "أخرى"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.ObservationCategories.AnyAsync(c => c.Code == code, ct))
                await db.ObservationCategories.AddAsync(new ObservationCategory(code, en, ar), ct);
    }

    private static async Task EnsureFeedbackCategories(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("technique", "Technique", "الأداء الفني"),
            ("endurance", "Endurance", "التحمل"),
            ("attitude", "Attitude", "السلوك"),
            ("punctuality", "Punctuality", "الالتزام بالمواعيد"),
            ("other", "Other", "أخرى"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.FeedbackCategories.AnyAsync(c => c.Code == code, ct))
                await db.FeedbackCategories.AddAsync(new FeedbackCategory(code, en, ar), ct);
    }

    private static async Task EnsureAttendanceStatuses(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("present", "Present", "حاضر"),
            ("late", "Late", "متأخر"),
            ("absent", "Absent", "غائب"),
            ("excused", "Excused", "بعذر"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.AttendanceStatuses.AnyAsync(s => s.Code == code, ct))
                await db.AttendanceStatuses.AddAsync(new AttendanceStatus(code, en, ar), ct);
    }

    private static async Task EnsureClubs(IdentityDbContext db, CancellationToken ct)
    {
        foreach (var (en, ar) in Clubs)
            if (!await db.Clubs.AnyAsync(c => c.NameEn == en, ct))
                await db.Clubs.AddAsync(new Club(en, ar), ct);
    }

    // Fixed Egyptian clubs — spec 2026-08-30-swimming-database-design §10 (English transliterations best-effort).
    private static readonly (string En, string Ar)[] Clubs =
    {
        ("Al Ahly", "الأهلي"),
        ("Zamalek", "الزمالك"),
        ("Pyramids", "بيراميدز"),
        ("Al Ittihad Alexandria", "الاتحاد السكندري"),
        ("Al Masry (Port Said)", "المصري البورسعيدي"),
        ("Ismaily", "الإسماعيلي"),
        ("Smouha", "سموحة"),
        ("ENPPI", "إنبي"),
        ("Wadi Degla", "وادي دجلة"),
        ("ZED FC", "زد إف سي"),
        ("Ceramica Cleopatra", "سيراميكا كليوباترا"),
        ("Modern Sport", "مودرن سبورت"),
        ("National Bank of Egypt", "البنك الأهلي المصري"),
        ("El Gouna", "الجونة"),
        ("Pharco", "فاركو"),
        ("Petrojet", "بتروجت"),
        ("Ghazl El Mahalla", "غزل المحلة"),
        ("Haras El Hodood", "حرس الحدود"),
        ("Tala'ea El Gaish", "طلائع الجيش"),
        ("Arab Contractors", "المقاولون العرب"),
        ("Ismailia Electricity", "كهرباء الإسماعيلية"),
        ("El Tersana", "الترسانة"),
        ("Tanta", "طنطا"),
        ("El Sekka El Hadeed (Railways)", "السكة الحديد"),
        ("Aswan", "أسوان"),
        ("El Qanah", "القناة"),
        ("La Viena", "لافيينا"),
        ("Abu Qir Fertilizers", "أبو قير للأسمدة"),
        ("Telecom Egypt", "المصرية للاتصالات"),
        ("Asyut Petroleum", "بترول أسيوط"),
        ("El Mansoura", "المنصورة"),
        ("Baladeyet El Mahalla", "بلدية المحلة"),
        ("El Dakhleya", "الداخلية"),
        ("El Entag El Harby", "الإنتاج الحربي"),
        ("Suez Team", "منتخب السويس"),
        ("El Shams", "الشمس"),
        ("El Nasr", "النصر"),
        ("El Obour", "العبور"),
        ("El Merreikh", "المريخ"),
        ("Port Fouad", "بورفؤاد"),
        ("Eastern Company", "إيسترن كومباني"),
        ("El Nogoom", "النجوم"),
        ("Gomhoreyet Shebin", "جمهورية شبين"),
        ("Benha", "بنها"),
        ("El Plastic", "البلاستيك"),
        ("Sporting Alexandria", "سبورتنج السكندري"),
        ("El Olympi", "الأوليمبي"),
        ("El Hammam", "الحمام"),
        ("Damanhour", "دمنهور"),
        ("Kafr El Sheikh", "كفر الشيخ"),
        ("Damietta", "دمياط"),
        ("Dekernes", "دكرنس"),
        ("Beni Ebeid", "بني عبيد"),
        ("Nabaroh", "نبروه"),
        ("El Minya", "المنيا"),
        ("El Fayoum", "الفيوم"),
        ("Misr El Makkasa", "مصر المقاصة"),
        ("Beni Suef Telecom", "تليفونات بني سويف"),
        ("Aluminium", "الألومنيوم"),
        ("Kima Aswan", "كيما أسوان"),
        ("Tahta", "طهطا"),
        ("Luxor", "الأقصر"),
        ("El Nasr Mining", "النصر للتعدين"),
        ("Asyut Cement", "أسمنت أسيوط"),
        ("Shoban Muslimeen Qena", "شبان مسلمين قنا"),
        ("El Badari", "البداري"),
        ("Aviation Club", "نادي الطيران"),
        ("Shooting Club", "نادي الصيد"),
        ("Palm Hills", "بالم هيلز"),
        ("6th of October Club", "نادي 6 أكتوبر"),
    };
}
