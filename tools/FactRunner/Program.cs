using System.Reflection;

// Прогонщик xunit-фактов через рефлексию. Штатный vstest в этой среде не работает:
// его testhost общается с родителем через именованные каналы, которые запрещены
// песочницей. Здесь тестовые сборки загружаются напрямую, а [Fact]-методы
// вызываются без посредника.

var assemblyPaths = args.Length > 0
    ? args
    : new[]
    {
        Path.Combine(AppContext.BaseDirectory, "Domain.Tests.dll"),
        Path.Combine(AppContext.BaseDirectory, "RemoteApp.Tests.dll")
    };

var passed = 0;
var failures = new List<string>();

foreach (var path in assemblyPaths)
{
    if (!File.Exists(path))
    {
        failures.Add($"сборка не найдена: {path}");
        Console.WriteLine($"!! сборка не найдена: {path}");
        continue;
    }

    var assembly = Assembly.LoadFrom(path);
    Console.WriteLine($"== {Path.GetFileName(path)} ==");

    foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName))
    {
        var facts = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "FactAttribute")
                        && m.GetParameters().Length == 0)
            .ToArray();

        if (facts.Length == 0)
            continue;

        object? instance;
        try
        {
            instance = Activator.CreateInstance(type);
        }
        catch (Exception ex)
        {
            failures.Add($"{type.Name}: не удалось создать экземпляр ({ex.GetType().Name})");
            Console.WriteLine($"  !! {type.Name}: не удалось создать экземпляр: {ex.Message}");
            continue;
        }

        Console.WriteLine($"  -- {type.Name}");

        foreach (var fact in facts)
        {
            var name = $"{type.Name}.{fact.Name}";
            try
            {
                var result = fact.Invoke(instance, null);
                if (result is Task task)
                    task.GetAwaiter().GetResult();

                passed++;
                Console.WriteLine($"     PASS  {fact.Name}");
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException { InnerException: not null } tie
                    ? tie.InnerException!
                    : ex;
                failures.Add($"{name}: {inner.GetType().Name}: {inner.Message}");
                Console.WriteLine($"     FAIL  {fact.Name}");
                Console.WriteLine($"           {inner.GetType().Name}: {inner.Message}");
            }
        }
    }
}

Console.WriteLine();
Console.WriteLine($"ИТОГО: PASS={passed} FAIL={failures.Count}");
if (failures.Count > 0)
{
    Console.WriteLine("Провалы:");
    foreach (var failure in failures)
        Console.WriteLine($"  - {failure}");
}

return failures.Count == 0 ? 0 : 1;
