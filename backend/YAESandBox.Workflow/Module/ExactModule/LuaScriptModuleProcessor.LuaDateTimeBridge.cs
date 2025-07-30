using System.Globalization;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Workflow.Module.ExactModule;

internal partial class LuaScriptModuleProcessor
{
    /// <summary>
    /// 日期时间模块的工厂类，暴露给 Lua 作为 'datetime'。
    /// </summary>
    private class LuaDateTimeBridge
    {
        /// <summary>
        /// 获取当前 UTC 时间。
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public LuaDateTimeObject utcnow() => new(DateTimeOffset.UtcNow);

        /// <summary>
        /// 获取当前本地时间（有时区偏移）。
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public LuaDateTimeObject now() => new(DateTimeOffset.Now);

        /// <summary>
        /// 从字符串解析日期时间。支持 ISO 8601 和自定义格式。
        /// </summary>
        /// <returns>如果解析成功，返回 LuaDateTimeObject；否则返回 null (nil in Lua)。</returns>
        // ReSharper disable once InconsistentNaming
        public LuaDateTimeObject? parse(string dateString, string? format = null)
        {
            if (string.IsNullOrEmpty(format))
            {
                // 尝试标准解析（非常灵活，能处理多种 ISO 格式）
                return DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                    ? new LuaDateTimeObject(result)
                    : null;
            }

            // 使用指定的格式进行精确解析
            return DateTimeOffset.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resultExact)
                ? new LuaDateTimeObject(resultExact)
                : null;
        }
    }

    /// <summary>
    /// 一个安全的 DateTimeOffset 包装器，暴露给 Lua。
    /// 这个对象是不可变的；所有 'add' 方法都返回一个新对象。
    /// </summary>
    private class LuaDateTimeObject(DateTimeOffset dateTimeOffset)
    {
        private DateTimeOffset Dto { get; } = dateTimeOffset;

        // --- 属性 ---
        public int year => this.Dto.Year;
        public int month => this.Dto.Month;
        public int day => this.Dto.Day;
        public int hour => this.Dto.Hour;
        public int minute => this.Dto.Minute;
        public int second => this.Dto.Second;
        public int millisecond => this.Dto.Millisecond;
        public int day_of_week => (int)this.Dto.DayOfWeek; // 0=Sunday, ..., 6=Saturday
        public int day_of_year => this.Dto.DayOfYear;

        // --- 操作方法 (返回新对象) ---
        public LuaDateTimeObject add_years(int years) => new(this.Dto.AddYears(years));
        public LuaDateTimeObject add_months(int months) => new(this.Dto.AddMonths(months));
        public LuaDateTimeObject add_days(double days) => new(this.Dto.AddDays(days));
        public LuaDateTimeObject add_hours(double hours) => new(this.Dto.AddHours(hours));
        public LuaDateTimeObject add_minutes(double minutes) => new(this.Dto.AddMinutes(minutes));
        public LuaDateTimeObject add_seconds(double seconds) => new(this.Dto.AddSeconds(seconds));

        // --- 格式化 ---
        /// <summary>
        /// 根据 .NET 格式化字符串来格式化日期。
        /// </summary>
        public string format(string formatString) => this.Dto.ToString(formatString, CultureInfo.InvariantCulture);

        /// <summary>
        /// 默认输出为 ISO 8601 格式，方便调试。
        /// </summary>
        public override string ToString() => this.Dto.ToString("o"); // Round-trip format
    }
}