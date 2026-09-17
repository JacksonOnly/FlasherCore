namespace FlasherCore.Sdk.Types
{
    /// <summary>
    /// 全局安全操作结果枚举
    /// </summary>
    public enum FlasherCoreResult : int
    {
        // --- 成功 ---
        Success = 0,

        [System.ComponentModel.Description("环境取证失败(检测到调试/虚拟化/时间回滚)")]
        EnvironmentCompromised = 101,

        [System.ComponentModel.Description("完整性校验失败(文件被篡改/未打补丁)")]
        IntegrityCheckFailed = 102,

        [System.ComponentModel.Description("授权许可证已过期")]
        LicenseExpired = 103,

        [System.ComponentModel.Description("请求时间戳误差过大(重放攻击防御)")]
        TimestampSkew = 104,

        [System.ComponentModel.Description("请求签名不匹配(Key错误)")]
        SignatureMismatch = 105,

        [System.ComponentModel.Description("会话无效或不存在")]
        SessionInvalid = 106,

        [System.ComponentModel.Description("会话已超时")]
        SessionExpired = 107,

        [System.ComponentModel.Description("核心:发现调试器")]
        CoreDebuggerDetected = 1021,

        [System.ComponentModel.Description("核心:自身数字签名无效")]
        CoreSignatureInvalid = 1022,

        [System.ComponentModel.Description("核心:内存解密失败")]
        CoreDecryptionFailed = 1023,

        InvalidPointer = 2001,
        ParseError = 2002,
        NotInitialized = 2003,
        BufferTooSmall = 2004,
        FileNotFound = 2005,
        InvalidFileName = 2006,
        DumpError = 2007,

        UnknownError = 999
    }
}