using Microsoft.EntityFrameworkCore;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<IEnumerable<User>> GetByCompanyIdAsync(int companyId)
    {
        return await _context.Users.Where(u => u.CompanyId == companyId).ToListAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        // 呼び出し元は常に GetByIdAsync 等で取得した、同じ DbContext に既にトラッキング
        // されているエンティティを渡す想定。Update() で再アタッチすると全プロパティが
        // Modified 扱いになり、変更していない CreatedAt（DBの timestamp without time zone
        // カラムから読み込んだ Kind=Unspecified な値）まで書き込み対象になってNpgsqlの
        // 「UTC以外のKindは書き込めない」チェックでDbUpdateExceptionになるため呼ばない。
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task DeleteAsync(int id)
    {
        var user = await GetByIdAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}

