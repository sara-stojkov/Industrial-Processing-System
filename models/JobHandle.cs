namespace IndustrialProcessingSystem.Models;

public class JobHandle
{
	public Guid Id { get; }
	public Task<int> Result => _tcs.Task;

	private readonly TaskCompletionSource<int> _tcs;

	public JobHandle(Guid id)
	{
		Id = id;
		_tcs = new TaskCompletionSource<int>();
	}

	public void Complete(int result) => _tcs.TrySetResult(result);

	public void Fail(Exception ex) => _tcs.TrySetException(ex);
}