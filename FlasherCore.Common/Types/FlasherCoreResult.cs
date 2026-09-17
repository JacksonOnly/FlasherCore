namespace FlasherCore.Common.Types;

public enum FlasherCoreResult : int
{
    Success = 0,
    [System.ComponentModel.Description("会话无效或不存在")]
    SessionInvalid = 106,
    IOException = 108,
    InvalidPointer = 2001,
    ParseError = 2002,
    NotInitialized = 2003,
    BufferTooSmall = 2004,
    FileNotFound = 2005,
    InvalidFileName = 2006,
    DumpError = 2007,
    UnknownError = 999,
}
