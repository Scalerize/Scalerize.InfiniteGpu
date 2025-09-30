using MediatR;
using InfiniteGPU.Backend.Features.Finance.Models;

namespace InfiniteGPU.Backend.Features.Finance.Queries;

public sealed record GetFinanceSummaryQuery(string UserId) : IRequest<FinanceSummaryDto>;