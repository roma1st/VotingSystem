using Bogus;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.Infrastructure.Data;

namespace VotingSystem.Api.Infrastructure;

public static class DbSeeder
{
    public static async Task SeedAsync(VotingDbContext context)
    {
        // Перевіряємо чи база вже наповнена (аби не дублювати 10k записів при перезапусках)
        if (await context.Elections.AnyAsync())
        {
            return;
        }

        Console.WriteLine("Починаємо генерацію 10 000+ тестових записів за допомогою Bogus...");

        // 1. Створюємо 20 виборів
        var electionFaker = new Faker<Election>()
            .RuleFor(e => e.Id, f => Guid.NewGuid())
            .RuleFor(e => e.Title, f => f.Company.CatchPhrase())
            .RuleFor(e => e.Description, f => f.Lorem.Paragraph())
            .RuleFor(e => e.StartDate, f => f.Date.Past(1).ToUniversalTime())
            .RuleFor(e => e.EndDate, f => f.Date.Future(1).ToUniversalTime())
            .RuleFor(e => e.Status, f => f.PickRandom<ElectionStatus>())
            .RuleFor(e => e.Type, f => f.PickRandom<ElectionType>());

        var elections = electionFaker.Generate(20);
        
        // Робимо кілька виборів конкретно "Активними" з фіксованими датами, 
        // щоб зручно було тестувати API.
        elections[0].Status = ElectionStatus.Active;
        elections[0].StartDate = DateTime.UtcNow.AddDays(-1);
        elections[0].EndDate = DateTime.UtcNow.AddDays(5);
        elections[0].Type = ElectionType.SingleChoice;

        elections[1].Status = ElectionStatus.Closed;
        elections[1].Type = ElectionType.RankedChoice;

        await context.Elections.AddRangeAsync(elections);
        await context.SaveChangesAsync();

        // 2. Створюємо Кандидатів (в середньому по 5-10 на вибори = ~150 кандидатів)
        var candidates = new List<Candidate>();
        var candidateFaker = new Faker<Candidate>()
            .RuleFor(c => c.Id, f => Guid.NewGuid())
            .RuleFor(c => c.Name, f => f.Name.FullName())
            .RuleFor(c => c.Description, f => f.Lorem.Sentence())
            .RuleFor(c => c.Party, f => f.Company.CompanyName())
            .RuleFor(c => c.PhotoUrl, f => f.Image.PicsumUrl());

        foreach (var election in elections)
        {
            var count = new Random().Next(4, 11);
            var electionCandidates = candidateFaker.Generate(count);
            foreach (var c in electionCandidates)
            {
                c.ElectionId = election.Id;
            }
            candidates.AddRange(electionCandidates);
        }

        await context.Candidates.AddRangeAsync(candidates);
        await context.SaveChangesAsync(); // Щоб зберегти ID кандидатів

        // 3. Створюємо Голоси (~9850 голосів, щоб в сумі було > 10 000 записів)
        // Для простоти генеруємо лише для закритих/активних виборів SingleChoice (elections[0], elections[2]) та Ranked
        var votes = new List<Vote>();
        var faker = new Faker();

        for (int i = 0; i < 9850; i++)
        {
            // Вибираємо випадкові вибори
            var randomElection = elections[faker.Random.Number(0, 19)];
            var electionCands = candidates.Where(c => c.ElectionId == randomElection.Id).ToList();
            if(!electionCands.Any()) continue;

            if (randomElection.Type == ElectionType.SingleChoice)
            {
                var randomCandidate = faker.PickRandom(electionCands);
                votes.Add(new Vote
                {
                    Id = Guid.NewGuid(),
                    ElectionId = randomElection.Id,
                    CandidateId = randomCandidate.Id,
                    VoterEmail = faker.Internet.Email(),
                    CastAt = faker.Date.Between(randomElection.StartDate, randomElection.EndDate).ToUniversalTime(),
                    Rank = null
                });
            }
            else // RankedChoice
            {
                // Для Borda Count треба згенерувати голоси для кількох кандидатів (імітація 1 Voter)
                var email = faker.Internet.Email();
                var shuffled = electionCands.OrderBy(x => Guid.NewGuid()).ToList();
                for (int rank = 1; rank <= shuffled.Count; rank++)
                {
                    votes.Add(new Vote
                    {
                        Id = Guid.NewGuid(),
                        ElectionId = randomElection.Id,
                        CandidateId = shuffled[rank - 1].Id,
                        VoterEmail = email,
                        CastAt = faker.Date.Between(randomElection.StartDate, randomElection.EndDate).ToUniversalTime(),
                        Rank = rank
                    });
                    i++; // Враховуємо згенеровані суб-голоси у загальний лічильник
                }
            }

            // Зберігаємо пакетами по 1000 щоб не перевантажити пам'ять EF
            if (votes.Count >= 1000)
            {
                // Не збереже, якщо Bogus випадково згенерує дублікат (VoterEmail + ElectionId + CandidateId), 
                // але шанс вкрай малий. Використовуємо ігнорування помилки індексу якщо що.
                await context.Votes.AddRangeAsync(votes);
                try { await context.SaveChangesAsync(); } catch { /* Ігнор дублювань від рандому */ }
                votes.Clear();
            }
        }

        if (votes.Any())
        {
            await context.Votes.AddRangeAsync(votes);
            try { await context.SaveChangesAsync(); } catch { }
        }

        Console.WriteLine("Генерацію 10 000+ записів завершено!");
    }
}
