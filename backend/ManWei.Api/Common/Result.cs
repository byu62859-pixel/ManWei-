namespace ManWei.Api.Common;

/// <summary>
/// 统一 API 返回结果
/// </summary>
public class Result<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public bool IsSuccess => Code == 200;

    public static Result<T> Success(T data, string message = "操作成功")
    {
        return new Result<T>
        {
            Code = 200,
            Message = message,
            Data = data
        };
    }

    public static Result<T> Fail(int code, string message)
    {
        return new Result<T>
        {
            Code = code,
            Message = message,
            Data = default
        };
    }

    public static Result<T> Fail(string message)
    {
        return new Result<T>
        {
            Code = 500,
            Message = message,
            Data = default
        };
    }
}

/// <summary>
/// 非泛型 Result（用于无返回数据的操作）
/// </summary>
public class Result
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;

    public bool IsSuccess => Code == 200;

    public static Result Success(string message = "操作成功")
    {
        return new Result
        {
            Code = 200,
            Message = message
        };
    }

    public static Result Fail(int code, string message)
    {
        return new Result
        {
            Code = code,
            Message = message
        };
    }

    public static Result Fail(string message)
    {
        return new Result
        {
            Code = 500,
            Message = message
        };
    }
}
