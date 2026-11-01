namespace Hwpx2Hwp.Core.Model;

/// <summary>파일 1건의 진행 상태. 취소되거나 처리되지 않은 파일은 Pending 으로 남는다(T4.2). D-4.</summary>
public enum JobState
{
    Pending,
    Running,
    Succeeded,
    Failed,
}
