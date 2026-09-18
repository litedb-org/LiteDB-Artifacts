using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using LiteDB;

public enum PhoneType { Mobile = 0, Landline = 1, Fax = 2 }

public class Phone
{
    public int Prefix { get; set; }
    public int Number { get; set; }
    public PhoneType Type { get; set; }
}

public class Address
{
    public string City { get; set; }
    public string Street { get; set; }
}

public class Product
{
    public ObjectId Id { get; set; }
    public string Name { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
    public int? NullableAge { get; set; }
    public decimal Balance { get; set; }
    public double Score { get; set; }
    public long BigNumber { get; set; }
    public bool Active { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
    public Guid ExternalId { get; set; }
    public ObjectId RefId { get; set; }
    public PhoneType FavoriteType { get; set; }
    public string[] Tags { get; set; }
    public List<int> Numbers { get; set; }
    public List<Phone> Phones { get; set; }
    public Address Address { get; set; }
    public Dictionary<string, int> Attributes { get; set; }
    public BsonValue RawValue { get; set; }
    public Product FavoriteProduct { get; set; }
}

/// <summary>
/// Translates a fixed corpus of lambdas and prints one deterministic line per lambda.
/// "cold" uses a fresh mapper for every value set. "warm" first translates the corpus
/// several times with value set 0 on ONE mapper, then with the requested set, so that
/// any shape cache is hit with different captured values than it was admitted with.
/// </summary>
internal static class Corpus
{
    [ThreadStatic] private static List<(string Text, LambdaExpression Lambda)> _items;
    private static List<(string Text, LambdaExpression Lambda)> Items => _items ?? (_items = new List<(string, LambdaExpression)>());

    private static void Expr<T, K>(Expression<Func<T, K>> e, [CallerArgumentExpression("e")] string text = null)
    {
        Items.Add((text, e));
    }

    private static void Build(int set)
    {
        Items.Clear();
        var name = set == 0 ? "John" : set == 1 ? "Mary" : null;
        var minAge = set == 0 ? 18 : set == 1 ? 21 : 0;
        var maxAge = set == 0 ? 65 : set == 1 ? 99 : -1;
        var prefix = set == 0 ? 40 : 43;
        var tag = set == 0 ? "vip" : set == 1 ? "new" : "";
        var count = set == 0 ? 3 : 7;
        var factor = set == 0 ? 2 : 5;
        var threshold = set == 0 ? 100m : 250.5m;
        var when = set == 0 ? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) : new DateTime(2024, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var guidValue = set == 0 ? new Guid("11111111-1111-1111-1111-111111111111") : new Guid("22222222-2222-2222-2222-222222222222");
        var idx = set == 0 ? 1 : 0;
        var years = set == 0 ? 5 : -2;
        var phoneType = set == 0 ? PhoneType.Mobile : PhoneType.Fax;
        var ids = set == 0 ? new[] { 1, 2, 3 } : set == 1 ? new[] { 30, 40 } : new int[0];
        IEnumerable<int> idSequence = set == 0 ? (IEnumerable<int>)new[] { 1, 2 } : set == 1 ? new List<int> { 30, 31, 32 } : null;
        var oidHex = set == 0 ? "5f6e7d8c9b0a1f2e3d4c5b6a" : "000000000000000000000001";
        int? nullableBound = set == 0 ? 30 : set == 1 ? 31 : (int?)null;
        Func<Customer, bool> isActivePredicate = c => c.Active;

        Expr<Customer, string>(x => x.Name);
        Expr<Customer, bool>(x => x.Active);
        Expr<Customer, int>(x => x.Age);
        Expr<Customer, bool>(x => x.Age > minAge);
        Expr<Customer, bool>(x => x.Age >= minAge);
        Expr<Customer, bool>(x => x.Age < maxAge);
        Expr<Customer, bool>(x => x.Age <= maxAge);
        Expr<Customer, bool>(x => x.Age == minAge);
        Expr<Customer, bool>(x => x.Age != minAge);
        Expr<Customer, bool>(x => x.Age > 18);
        Expr<Customer, bool>(x => x.Age > 19);
        Expr<Customer, bool>(x => x.Name == name);
        Expr<Customer, bool>(x => x.Name != name);
        Expr<Customer, bool>(x => x.Name == null);
        Expr<Customer, bool>(x => x.NullableAge == nullableBound);
        Expr<Customer, bool>(x => x.NullableAge > nullableBound);
        Expr<Customer, bool>(x => x.Age > minAge && x.Age < maxAge);
        Expr<Customer, bool>(x => x.Age > minAge || x.Name == name);
        Expr<Customer, bool>(x => !x.Active);
        Expr<Customer, bool>(x => !(x.Age > minAge && x.Active));
        Expr<Customer, decimal>(x => x.Balance + threshold);
        Expr<Customer, decimal>(x => x.Balance - threshold);
        Expr<Customer, decimal>(x => x.Balance * factor);
        Expr<Customer, decimal>(x => x.Balance / factor);
        Expr<Customer, int>(x => x.Age % count);
        Expr<Customer, bool>(x => -x.Balance > 100m);
        Expr<Customer, bool>(x => x.Age > minAge + 1);
        Expr<Customer, bool>(x => (int)x.FavoriteType == 1);
        Expr<Customer, bool>(x => x.FavoriteType == phoneType);
        Expr<Customer, bool>(x => x.FavoriteType == PhoneType.Landline);
        Expr<Customer, string>(x => x.Name.ToUpper());
        Expr<Customer, string>(x => x.Name.ToUpperInvariant());
        Expr<Customer, string>(x => x.Name.ToLower());
        Expr<Customer, string>(x => x.Name.ToLowerInvariant());
        Expr<Customer, string>(x => x.Name.Trim());
        Expr<Customer, string>(x => x.Name.TrimStart());
        Expr<Customer, string>(x => x.Name.TrimEnd());
        Expr<Customer, int>(x => x.Name.Length);
        Expr<Customer, bool>(x => x.Name.Contains(tag));
        Expr<Customer, bool>(x => x.Name.StartsWith(tag));
        Expr<Customer, bool>(x => x.Name.EndsWith(tag));
        Expr<Customer, bool>(x => x.Name.Equals(tag));
        Expr<Customer, bool>(x => x.Name.Contains("lit"));
        Expr<Customer, bool>(x => x.Name.Contains("other"));
        Expr<Customer, string>(x => x.Name.Substring(idx));
        Expr<Customer, string>(x => x.Name.Substring(idx, count));
        Expr<Customer, string>(x => x.Name.Substring(1));
        Expr<Customer, string>(x => x.Name.Substring(2));
        Expr<Customer, int>(x => x.Name.IndexOf(tag));
        Expr<Customer, int>(x => x.Name.IndexOf(tag, idx));
        Expr<Customer, string>(x => x.Name.PadLeft(count, ' '));
        Expr<Customer, string>(x => x.Name.Replace(tag, "zz"));
        Expr<Customer, bool>(x => string.IsNullOrEmpty(x.Name));
        Expr<Customer, bool>(x => string.IsNullOrWhiteSpace(x.Name));
        Expr<Customer, string>(x => x.Name + " " + x.Address.City);
        Expr<Customer, string>(x => string.Empty);
        Expr<Customer, DateTime>(x => x.Created);
        Expr<Customer, DateTime>(x => DateTime.Now);
        Expr<Customer, DateTime>(x => DateTime.UtcNow);
        Expr<Customer, DateTime>(x => DateTime.Today);
        Expr<Customer, int>(x => x.Created.Year);
        Expr<Customer, int>(x => x.Created.Month);
        Expr<Customer, int>(x => x.Created.Day);
        Expr<Customer, int>(x => x.Created.Hour);
        Expr<Customer, int>(x => x.Created.Minute);
        Expr<Customer, int>(x => x.Created.Second);
        Expr<Customer, DateTime>(x => x.Created.Date);
        Expr<Customer, DateTime>(x => x.Created.AddYears(years));
        Expr<Customer, DateTime>(x => x.Created.AddMonths(years));
        Expr<Customer, DateTime>(x => x.Created.AddDays(years));
        Expr<Customer, DateTime>(x => x.Created.AddHours(years));
        Expr<Customer, DateTime>(x => x.Created.AddMinutes(years));
        Expr<Customer, DateTime>(x => x.Created.AddSeconds(years));
        Expr<Customer, DateTime>(x => x.Created.ToUniversalTime());
        Expr<Customer, DateTime>(x => x.Created.ToLocalTime());
        Expr<Customer, DateTime>(x => x.Created.ToLocalTime().ToUniversalTime());
        Expr<Customer, string>(x => x.Created.ToString());
        Expr<Customer, string>(x => x.Created.ToString("yyyy-MM-dd"));
        Expr<Customer, bool>(x => x.Created.Equals(when));
        Expr<Customer, bool>(x => x.Created > when);
        Expr<Customer, bool>(x => x.Updated > when);
        Expr<Customer, DateTime>(x => new DateTime(2020, 1, 1));
        Expr<Customer, string>(x => x.Age.ToString());
        Expr<Customer, string>(x => x.Age.ToString("D3"));
        Expr<Customer, bool>(x => x.Age.Equals(minAge));
        Expr<Customer, int>(x => Convert.ToInt32(x.Score));
        Expr<Customer, long>(x => Convert.ToInt64(x.Score));
        Expr<Customer, double>(x => Convert.ToDouble(x.Age));
        Expr<Customer, string>(x => Convert.ToString(x.Age));
        Expr<Customer, int>(x => (int)x.Score);
        Expr<Customer, long>(x => (long)x.Balance);
        Expr<Customer, double>(x => Math.Abs(x.Score));
        Expr<Customer, double>(x => Math.Pow(x.Score, factor));
        Expr<Customer, double>(x => Math.Round(x.Score, count));
        Expr<Customer, string>(x => x.ExternalId.ToString());
        Expr<Customer, bool>(x => x.ExternalId.Equals(guidValue));
        Expr<Customer, bool>(x => x.ExternalId == guidValue);
        Expr<Customer, bool>(x => x.ExternalId == Guid.Empty);
        Expr<Customer, Guid>(x => Guid.NewGuid());
        Expr<Customer, Guid>(x => Guid.Parse(oidHex));
        Expr<Customer, string>(x => x.RefId.ToString());
        Expr<Customer, bool>(x => x.RefId.Equals(ObjectId.Empty));
        Expr<Customer, DateTime>(x => x.RefId.CreationTime);
        Expr<Customer, ObjectId>(x => new ObjectId(oidHex));
        Expr<Customer, bool>(x => Regex.IsMatch(x.Name, "^J"));
        Expr<Customer, string[]>(x => Regex.Split(x.Name, " "));
        Expr<Customer, bool>(x => x.NullableAge.HasValue);
        Expr<Customer, int>(x => x.NullableAge.Value);
        Expr<Customer, bool>(x => x.RawValue.IsNull);
        Expr<Customer, bool>(x => x.RawValue.IsString);
        Expr<Customer, bool>(x => x.RawValue.IsNumber);
        Expr<Customer, int>(x => x.Phones.Count);
        Expr<Customer, int>(x => x.Numbers.Count());
        Expr<Customer, int>(x => x.Tags.Length);
        Expr<Customer, bool>(x => x.Phones.Any());
        Expr<Customer, bool>(x => x.Phones.Any(p => p.Prefix == prefix));
        Expr<Customer, bool>(x => x.Phones.Any(p => p.Number > 100));
        Expr<Customer, bool>(x => x.Phones.All(p => p.Prefix == prefix));
        Expr<Customer, bool>(x => x.Numbers.Contains(idx));
        Expr<Customer, bool>(x => ids.Contains(x.Age));
        Expr<Customer, bool>(x => idSequence.Contains(x.Age));
        Expr<Customer, IEnumerable<Phone>>(x => x.Phones.Where(p => p.Prefix == prefix));
        Expr<Customer, IEnumerable<int>>(x => x.Phones.Select(p => p.Number));
        Expr<Customer, int>(x => x.Phones.Count(p => p.Type == phoneType));
        Expr<Customer, int>(x => x.Phones.Sum(p => p.Number));
        Expr<Customer, double>(x => x.Phones.Average(p => p.Number));
        Expr<Customer, int>(x => x.Phones.Max(p => p.Number));
        Expr<Customer, int>(x => x.Phones.Min(p => p.Number));
        Expr<Customer, Phone>(x => x.Phones.First());
        Expr<Customer, Phone>(x => x.Phones.First(p => p.Prefix == prefix));
        Expr<Customer, Phone>(x => x.Phones.FirstOrDefault());
        Expr<Customer, Phone>(x => x.Phones.Last());
        Expr<Customer, Phone>(x => x.Phones.LastOrDefault());
        Expr<Customer, Phone>(x => x.Phones.Single());
        Expr<Customer, Phone>(x => x.Phones.ElementAt(idx));
        Expr<Customer, Phone>(x => x.Phones[0]);
        Expr<Customer, Phone>(x => x.Phones[1]);
        Expr<Customer, int>(x => x.Numbers[idx]);
        Expr<Customer, string>(x => x.Tags[idx]);
        Expr<Customer, int>(x => x.Attributes["score"]);
        Expr<Customer, int>(x => x.Attributes["other"]);
        Expr<Customer, Phone[]>(x => x.Phones.ToArray());
        Expr<Customer, List<int>>(x => x.Numbers.ToList());
        Expr<Customer, bool>(x => x.Phones.Select(p => p.Number).Any(n => n > 5));
        Expr<Customer, bool>(x => x.Phones.Select(p => p.Number).Any(n => n == count));
        Expr<Customer, bool>(x => x.Tags.Any(t => t.StartsWith(tag)));
        Expr<Customer, bool>(x => x.Tags.Any(t => t.Contains(tag)));
        Expr<Customer, bool>(x => x.Tags.Any(t => t.EndsWith(tag)));
        Expr<Customer, string>(x => x.Age > minAge ? "adult" : "minor");
        Expr<Customer, string>(x => x.Email ?? x.Name);
        Expr<Customer, object>(x => new { x.Id, x.Name, x.Age });
        Expr<Customer, object>(x => new { Full = x.Name + " " + x.Email, x.Age });
        Expr<Customer, Customer>(x => new Customer { Id = x.Id, Name = x.Name.ToUpper() });
        Expr<Customer, int[]>(x => new[] { 1, 2, 3 });
        Expr<Customer, int[]>(x => new[] { x.Age, minAge, maxAge });
        Expr<Customer, object[]>(x => new object[] { x.Phones.Where(p => p.Prefix == prefix) });
        Expr<Customer, object>(x => new { City = x.Address.City, Count = x.Phones.Count(p => p.Type == phoneType), List = x.Phones.Where(p => p.Number > x.Age).Select(p => p.Number).ToArray() });
        Expr<Customer, bool>(x => isActivePredicate(x));
        Expr<Customer, bool>(x => x.RawValue is BsonArray);
        Expr<Customer, int>(x => x.Phones.Count + x.Numbers.Count());
        Expr<Customer, bool>(x => x.Age > minAge && (x.Active || x.Balance > threshold));
        Expr<Customer, bool>(x => x.Created.ToLocalTime().ToUniversalTime() == when.ToUniversalTime());
        Expr<Customer, string>(x => x.Address.City);
        Expr<Customer, string>(x => x.FavoriteProduct.Name);
        Expr<Customer, bool>(x => x.Address.City == name && x.Address.Street != tag);
    }

    private static readonly Regex NonDeterministic = new Regex("NOW|TODAY|GUID\\(\\)|RANDOM|OBJECTID\\(\\)", RegexOptions.IgnoreCase);

    private static BsonDocument SampleDocument(BsonMapper mapper)
    {
        return mapper.ToDocument(new Customer
        {
            Id = 7, Name = "John vip", Email = null, Age = 30, NullableAge = 30, Balance = 120.5m, Score = 9.25,
            BigNumber = 1L << 40, Active = true, Created = new DateTime(2020, 1, 1, 10, 30, 15, DateTimeKind.Utc),
            Updated = null, ExternalId = new Guid("11111111-1111-1111-1111-111111111111"),
            RefId = new ObjectId("5f6e7d8c9b0a1f2e3d4c5b6a"), FavoriteType = PhoneType.Fax,
            Tags = new[] { "vip", "new" }, Numbers = new List<int> { 0, 1, 2, 3 },
            Phones = new List<Phone> { new Phone { Prefix = 40, Number = 555, Type = PhoneType.Mobile }, new Phone { Prefix = 43, Number = 7, Type = PhoneType.Fax } },
            Address = new Address { City = "John", Street = "Main" },
            Attributes = new Dictionary<string, int> { ["score"] = 5, ["other"] = 6 },
            RawValue = "text", FavoriteProduct = new Product { Id = ObjectId.Empty, Name = "Thing" }
        });
    }

    private static string Describe(BsonMapper mapper, LambdaExpression lambda, BsonDocument sample)
    {
        try
        {
            var method = typeof(BsonMapper).GetMethods().First(m => m.Name == "GetExpression" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(lambda.Parameters[0].Type, lambda.ReturnType);
            var expr = (BsonExpression)method.Invoke(mapper, new object[] { lambda });
            var useSource = typeof(BsonExpression).GetProperty("UseSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(expr);
            var parameters = JsonSerializer.Serialize(expr.Parameters);
            var fields = string.Join(",", expr.Fields.OrderBy(f => f, StringComparer.Ordinal));
            string result;
            if (NonDeterministic.IsMatch(expr.Source)) result = "(non-deterministic)";
            else
            {
                try { result = JsonSerializer.Serialize(new BsonArray(expr.Execute(sample).ToArray())); }
                catch (Exception ex) { result = "EXEC-THROW " + (ex.InnerException ?? ex).GetType().Name; }
            }
            return $"src={expr.Source} | type={expr.Type} | immutable={expr.IsImmutable} | scalar={expr.IsScalar} | useSource={useSource} | fields=[{fields}] | params={parameters} | result={result}";
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            return "TRANSLATE-THROW " + inner.GetType().Name;
        }
    }

    public static string[] Snapshot(BsonMapper mapper, int set)
    {
        var sample = SampleDocument(new BsonMapper());
        Build(set);
        return Items.Select(item => Describe(mapper, item.Lambda, sample)).ToArray();
    }

    public static int Run(string mode, int set)
    {
        var mapper = new BsonMapper();
        var sample = SampleDocument(mapper);
        if (mode == "warm")
        {
            for (var round = 0; round < 4; round++)
            {
                Build(0);
                foreach (var item in Items) Describe(mapper, item.Lambda, sample);
            }
        }
        Build(set);
        var index = 0;
        foreach (var item in Items)
        {
            Console.WriteLine($"[{index++:000}] {item.Text}");
            Console.WriteLine("      " + Describe(mapper, item.Lambda, sample));
        }
        return 0;
    }
}
