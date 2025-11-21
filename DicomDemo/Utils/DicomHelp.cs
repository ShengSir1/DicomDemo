namespace DicomDemo.Utils
{
    public class DicomHelp
    {
        /// <summary>
        /// 使用 fo-dicom 的编码映射读取 DICOM 字符串并按 SpecificCharacterSet 解码。
        /// </summary>
        /// <param name="ds">要读取的 DICOM 数据集。</param>
        /// <param name="tag">要读取的 DICOM 标签（例如 <see cref="DicomTag.PatientName"/>）。</param>
        /// <returns>解码后的字符串；在出错时回退为库的 <c>GetString</c> 结果。</returns>
        [Description("使用 fo-dicom 库的编码映射来读取 DICOM 字符串，处理 SpecificCharacterSet 指定的编码")]
        public static string ReadDicomStringWithLibraryEncoding(DicomDataset ds, DicomTag tag)
        {
            // 优先尝试用 fo-dicom 的映射将 SpecificCharacterSet 转为 Encoding[]
            try
            {
                // 先尝试拿到原始字节
                byte[] rawBytes;
                try
                {
                    rawBytes = ds.GetValues<byte>(tag);
                }
                catch
                {
                    // 如果不能拿到原始字节，则回退到库的高层 API
                    return ds.GetString(tag);
                }

                if (rawBytes == null || rawBytes.Length == 0)
                    return ds.GetString(tag);

                // 获取 SpecificCharacterSet（可能为多个值）
                string[] charsets = null;
                if (ds.Contains(DicomTag.SpecificCharacterSet))
                {
                    try { charsets = ds.GetValues<string>(DicomTag.SpecificCharacterSet); } catch { charsets = null; }
                }

                // 使用 fo-dicom 的映射方法获得 Encoding 列表
                Encoding enc = null;
                if (charsets != null && charsets.Length > 0)
                {
                    var encs = DicomEncoding.GetEncodings(charsets);
                    enc = encs?.FirstOrDefault();
                }

                // 如果没有指定字符集，fo-dicom 可能使用默认映射（可按需要设为 UTF8）
                enc ??= Encoding.UTF8;

                // 如果文件标记为 ISO_IR 100（ISO-8859-1），但你知道文件实际是中文编码（例如 GBK/GB18030），可以在此处强制覆盖：
                if (charsets != null && charsets.Contains("ISO_IR 100")) enc = Encoding.GetEncoding("GB18030");
                // 上面一行在你确认标记错误且知道实际编码时解除注释

                var decoded = enc.GetString(rawBytes).TrimEnd('\0', ' ');
                return decoded;
            }
            catch
            {
                return ds.GetString(tag);
            }
        }

        /// <summary>
        /// 将用户以 GB18030 编码的字符串转换为 ISO_IR 100 (ISO-8859-1) 的字节到字符串映射。
        /// 实现方式：先用 GB18030 将字符串编码为字节，然后用 ISO-8859-1 按字节到字符的 1:1 映射构造字符串。
        /// 这种方式常用于将包含中文的字节流在标记为 ISO_IR 100 的 DICOM 数据集中以不丢失原始字节的方式保存。
        /// 注意：ISO-8859-1 本身无法表示中文字符，这里只是把原始 GB18030 字节封装为一个 ISO-8859-1 字符串，以便库按字节写入。
        /// </summary>
        /// <param name="input">用户输入的字符串（期望为 Unicode 中文），返回可放入标记为 ISO_IR 100 的 DICOM 元素的字符串表示。</param>
        /// <returns>经转换后的字符串；若输入为 null 或 空则直接返回原值。</returns>
        [Description("将 GB18030 编码的字符串转换为 ISO_IR 100 (ISO-8859-1)")]
        public static string ConvertGb18030ToIsoIr100(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // 将输入用 GB18030 编成字节
            var gbBytes = Encoding.GetEncoding("GB18030").GetBytes(input);

            // 使用 ISO-8859-1 (Latin1) 将字节按 1:1 映射为字符串
            var isoString = Encoding.GetEncoding("ISO-8859-1").GetString(gbBytes);

            return isoString;
        }

        /// <summary>
        /// 将输入文本按 VR 规则拆分并尝试校验/转换为适合 AddOrUpdate 的值数组。
        /// 当前实现对常用文本 VR 做简单校验（如 DA 的 YYYYMMDD），其他 VR 视为文本。
        /// </summary>
        public static object[] ConvertStringToValuesForVr(string raw, DicomVR vr)
        {
            if (string.IsNullOrEmpty(raw)) return Array.Empty<object>();
            // split multi-value using backslash
            var parts = raw.Split('\\').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            if (parts.Length == 0) return Array.Empty<object>();

            // simple validators and conversions
            switch (vr.Code)
            {
                case "DA": // Date
                    foreach (var p in parts)
                    {
                        if (p.Length != 8 || !DateTime.TryParseExact(p, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out _))
                            throw new FormatException("DA 格式应为 YYYYMMDD，例如 20250131。");
                    }
                    return parts.Cast<object>().ToArray();
                case "TM": // Time
                    foreach (var p in parts)
                    {
                        if (p.Length < 2) throw new FormatException("TM 格式似乎不正确。");
                    }
                    return parts.Cast<object>().ToArray();
                case "IS": // Integer String
                    var listIs = new object[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (!int.TryParse(parts[i], out _)) throw new FormatException("IS (整数字符串) 应为整数。多个值请用\\分隔。");
                        listIs[i] = parts[i];
                    }
                    return listIs;
                case "DS": // Decimal String
                    var listDs = new object[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (!double.TryParse(parts[i], out _)) throw new FormatException("DS 应为数字。多个值请用\\分隔。");
                        listDs[i] = parts[i];
                    }
                    return listDs;
                default:
                    return parts.Select(p => (object)ConvertGb18030ToIsoIr100(p)).ToArray();
            }
        }

        /// <summary>
        /// 从指定的输入字符串中解析DICOM标签，支持多种常见的DICOM标签格式。
        /// </summary>
        /// <remarks>
        /// 此方法接受以下格式的DICOM标签表示：组和元素以逗号分隔的十六进制数表示（例如，“0010,0010”），一个连续的8位十六进制数字符串（例如“00100010”）或标准DICOM关键字（例如“患者姓名”）。
        /// 如果输入不匹配、如果任何支持的格式或无效，该方法将返回null。
        /// </remarks>
        /// <param name="input">表示DICOM标签的输入字符串。支持的格式包括“gggg，eeee”，一个8位十六进制字符串或DICOM关键字。前导和尾随空格和括号将被忽略。</param>
        /// <returns>如果输入有效，则表示解析的Dicom标签的Dicom标签；否则为空</returns>
        public static DicomTag? ParseDicomTagFromInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            input = input.Trim();
            if (input.StartsWith('(') && input.EndsWith(')')) input = input.Substring(1, input.Length - 2).Trim();

            // Try "gggg,eeee"
            if (input.Contains(','))
            {
                var parts = input.Split(',').Select(p => p.Trim()).ToArray();
                if (parts.Length == 2 && parts[0].Length == 4 && parts[1].Length == 4 && IsHex(parts[0]) && IsHex(parts[1]))
                {
                    var group = Convert.ToUInt16(parts[0], 16);
                    var element = Convert.ToUInt16(parts[1], 16);
                    return new DicomTag(group, element);
                }
            }

            // Try continuous 8-digit hex
            if (input.Length == 8 && IsHex(input))
            {
                var group = Convert.ToUInt16(input.Substring(0, 4), 16);
                var element = Convert.ToUInt16(input.Substring(4, 4), 16);
                return new DicomTag(group, element);
            }

            // Try keyword/name
            try
            {
                return DicomTag.Parse(input);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 确定指定的字符串是否仅由有效的十六进制数字组成。
        /// </summary>
        /// <param name="s">要验证的字符串。可以为null或空。</param>
        /// <returns>如果字符串中的所有字符都是有效的十六进制数字（0-9、A-F、A-F），则为true；否则为假。</returns>
        private static bool IsHex(string s) => s.All(c => Uri.IsHexDigit(c));

        /// <summary>
        /// 返回与DICOM患者性别值对应的显示文本。
        /// </summary>
        /// <param name="dcmSexValue">要转换的DICOM患者性别代码。常见的值是“M”代表男性，“F”代表女性，“O”代表其他，以及“U”代表未知。</param>
        /// <returns>“M”代表男性，“F”代表女性，“O”代表其他，以及“U”代表未知。</returns>
        public static string GetPatientSexDisplayText(string dcmSexValue)
        {
            return dcmSexValue?.ToUpperInvariant() switch
            {
                "M" => "男",
                "F" => "女",
                "O" => "其他",
                "U" or "" or null => "未知",
                _ => $"未知代码 ({dcmSexValue})" // 默认情况，处理意外值
            };
        }
    }
}
