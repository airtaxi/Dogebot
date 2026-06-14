namespace Dogebot.Server.Services;

public interface ILottoService : IDengAiCallableService
{
    string CreateLottoMessage(int count = 1);
}
