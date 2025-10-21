using System.Text;

namespace PGD.Jobs.SourceGenerator.CodeGen
{
    /// <summary>
    /// 代码构建辅助类
    /// </summary>
    internal class CodeBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indentLevel = 0;
        private const string IndentString = "    "; // 4个空格

        /// <summary>
        /// 添加一行代码
        /// </summary>
        public CodeBuilder AppendLine(string? line = null)
        {
            if (string.IsNullOrEmpty(line))
            {
                _sb.AppendLine();
            }
            else
            {
                // 添加缩进
                for (int i = 0; i < _indentLevel; i++)
                {
                    _sb.Append(IndentString);
                }
                _sb.AppendLine(line);
            }
            return this;
        }

        /// <summary>
        /// 添加代码但不换行
        /// </summary>
        public CodeBuilder Append(string text)
        {
            _sb.Append(text);
            return this;
        }

        /// <summary>
        /// 增加缩进
        /// </summary>
        public CodeBuilder IncreaseIndent()
        {
            _indentLevel++;
            return this;
        }

        /// <summary>
        /// 减少缩进
        /// </summary>
        public CodeBuilder DecreaseIndent()
        {
            if (_indentLevel > 0)
                _indentLevel--;
            return this;
        }

        /// <summary>
        /// 添加左大括号并增加缩进
        /// </summary>
        public CodeBuilder OpenBrace()
        {
            AppendLine("{");
            IncreaseIndent();
            return this;
        }

        /// <summary>
        /// 减少缩进并添加右大括号
        /// </summary>
        public CodeBuilder CloseBrace()
        {
            DecreaseIndent();
            AppendLine("}");
            return this;
        }

        /// <summary>
        /// 添加注释
        /// </summary>
        public CodeBuilder AppendComment(string comment)
        {
            AppendLine($"// {comment}");
            return this;
        }

        /// <summary>
        /// 添加 XML 文档注释
        /// </summary>
        public CodeBuilder AppendXmlComment(string summary)
        {
            AppendLine("/// <summary>");
            AppendLine($"/// {summary}");
            AppendLine("/// </summary>");
            return this;
        }

        /// <summary>
        /// 获取生成的代码
        /// </summary>
        public override string ToString()
        {
            return _sb.ToString();
        }

        /// <summary>
        /// 清空内容
        /// </summary>
        public void Clear()
        {
            _sb.Clear();
            _indentLevel = 0;
        }
    }
}

