using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace PGD.Jobs.SourceGenerator.CodeGen
{
    /// <summary>
    /// 模板引擎 - 处理模板加载和占位符替换
    /// </summary>
    internal class TemplateEngine
    {
        private static string? _jobTemplate;

        /// <summary>
        /// 从嵌入资源中加载 Job 模板
        /// </summary>
        private static string LoadJobTemplate()
        {
            if (_jobTemplate != null)
                return _jobTemplate;

            var assembly = typeof(TemplateEngine).Assembly;
            var resourceName = "PGD.Jobs.SourceGenerator.CodeGen.Templates.JobTemplate.txt";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Failed to load embedded template: {resourceName}");
                }

                using (var reader = new StreamReader(stream))
                {
                    _jobTemplate = reader.ReadToEnd();
                }
            }

            return _jobTemplate;
        }

        /// <summary>
        /// 应用模板，替换所有占位符
        /// </summary>
        public static string ApplyTemplate(TemplateData data)
        {
            var result = LoadJobTemplate();

            // 替换命名空间
            if (!string.IsNullOrEmpty(data.Namespace))
            {
                result = result.Replace(Constants.Placeholders.NAMESPACE_BEGIN, $"namespace {data.Namespace}\n{{");
                result = result.Replace(Constants.Placeholders.NAMESPACE_END, "}");
            }
            else
            {
                result = result.Replace(Constants.Placeholders.NAMESPACE_BEGIN, string.Empty);
                result = result.Replace(Constants.Placeholders.NAMESPACE_END, string.Empty);
            }

            // 生成 usings
            var usingsBuilder = new StringBuilder();
            foreach (var usingNamespace in data.Usings)
            {
                usingsBuilder.AppendLine($"using {usingNamespace};");
            }
            result = result.Replace(Constants.Placeholders.USINGS, usingsBuilder.ToString());

            // 替换基本信息
            result = result.Replace(Constants.Placeholders.JOB_NAME, data.JobName);
            result = result.Replace(Constants.Placeholders.WRAPPER_NAME, data.WrapperName);
            result = result.Replace(Constants.Placeholders.EXTENSIONS_NAME, data.ExtensionsName);

            // 替换代码片段
            result = result.Replace(Constants.Placeholders.WRAPPER_FIELDS, data.WrapperFields);
            result = result.Replace(Constants.Placeholders.WRAPPER_EXECUTE_BODY, data.WrapperExecuteBody);
            result = result.Replace(Constants.Placeholders.EXECUTE_GENERATED_SIGNATURE, data.ExecuteGeneratedSignature);
            result = result.Replace(Constants.Placeholders.EXECUTE_GENERATED_CALL, data.ExecuteGeneratedCall);
            result = result.Replace(Constants.Placeholders.EXTENSION_STATIC_FIELDS, data.ExtensionStaticFields);
            result = result.Replace(Constants.Placeholders.SCHEDULE_PARALLEL_BODY, data.ScheduleParallelBody);

            return result;
        }

        /// <summary>
        /// 辅助方法：生成缩进
        /// </summary>
        public static string Indent(int level = 1)
        {
            return new string(' ', level * 4);
        }

        /// <summary>
        /// 辅助方法：为多行文本添加缩进
        /// </summary>
        public static string IndentLines(string text, int indentLevel)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var indent = Indent(indentLevel);
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var builder = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    builder.Append(indent);
                    builder.AppendLine(line);
                }
                else
                {
                    builder.AppendLine();
                }
            }

            // 移除最后的换行符
            if (builder.Length > 0 && builder[builder.Length - 1] == '\n')
            {
                builder.Length--;
                if (builder.Length > 0 && builder[builder.Length - 1] == '\r')
                {
                    builder.Length--;
                }
            }

            return builder.ToString();
        }
    }
}

