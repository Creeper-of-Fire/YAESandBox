using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection; // 需要引入 System.Reflection

public class RemoveHiddenFromJsonFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // 用于存储需要移除的路径和操作 (Path Key, Http Method)
        var pathsToRemove = new List<(string PathKey, OperationType HttpMethod)>();

        // 遍历所有被 API Explorer 发现的描述
        foreach (var apiDescription in context.ApiDescriptions)
        {
            // 检查方法或其所在的控制器类是否标记了我们的自定义属性
            bool isHiddenFromJson = apiDescription.TryGetMethodInfo(out MethodInfo methodInfo) &&
                                    (methodInfo.GetCustomAttribute<HiddenFromJsonApiAttribute>() != null ||
                                     methodInfo.DeclaringType?.GetCustomAttribute<HiddenFromJsonApiAttribute>() != null);

            if (isHiddenFromJson)
            {
                // Swashbuckle 通常使用小写的 HTTP 方法名作为键
                var httpMethod = apiDescription.HttpMethod?.ToLowerInvariant();
                if (string.IsNullOrEmpty(httpMethod) || string.IsNullOrEmpty(apiDescription.RelativePath))
                {
                    continue; // 无效的描述，跳过
                }

                // 将路径模板格式化为 Swagger 的路径键格式 (通常是 / 开头)
                // 注意：RelativePath 可能不带前导 /，需要确保一致
                var pathKey = "/" + apiDescription.RelativePath.TrimStart('/');

                // 尝试将 HTTP 方法字符串转换为 OperationType 枚举
                if (Enum.TryParse<OperationType>(httpMethod, true, out var operationType))
                {
                    pathsToRemove.Add((pathKey, operationType));
                }
                else
                {
                    // 记录一个警告或错误，因为无法识别 HTTP 方法
                    Console.WriteLine($"[WARN] RemoveHiddenFromJsonFilter: 无法识别的 HTTP 方法 '{httpMethod}' 用于路径 '{pathKey}'");
                }
            }
        }

        // 现在从 swaggerDoc 中移除标记的操作
        foreach (var (pathKey, httpMethod) in pathsToRemove)
        {
            if (swaggerDoc.Paths.TryGetValue(pathKey, out var pathItem))
            {
                // 根据 OperationType 移除对应的操作
                pathItem.Operations.Remove(httpMethod);

                // 如果移除后这个 PathItem 没有任何操作了，可以选择将整个路径也移除
                if (!pathItem.Operations.Any())
                {
                    swaggerDoc.Paths.Remove(pathKey);
                }
            }
        }
    }
}

/// <summary>
/// 指示此 API 端点应仅在 Swagger UI 中可见，
/// 但不应包含在生成的 swagger.json 文件中。
/// 主要用于内部测试或调试目的的 API。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class HiddenFromJsonApiAttribute : Attribute;