using System;
using System.IO;
using ExtractionShooter.DataPipeline;

// 실제 생성기를 실행한다. Unity를 띄우거나 에디터 로그를 호출하지 않는다.
public static class BindingGenerationHost
{
    public static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        foreach (Type type in new[] { typeof(WeaponDefinition), typeof(AmmoDefinition), typeof(MedicineDefinition), typeof(ArmorDefinition), typeof(HelmetDefinition) })
        {
            string name = type.Name.Replace("Definition", "");
            string code = CsvBindingGenerator.Generate(type);
            string destination = Path.Combine(root, "Assets/Scripts/DataPipeline/Csv", "Generated" + name + "Binding.cs");
            // 생성 결과만 저장한다. 사용자가 작성하는 런타임 코드에는 쓰지 않는다.
            if (!File.Exists(destination) || File.ReadAllText(destination).Replace("\r\n", "\n") != code.Replace("\r\n", "\n")) File.WriteAllText(destination, code);
            Console.WriteLine("Generated" + name + "Binding");
        }
    }
}
