using AutoMapper;
using ISP.Application.DTOs.Auth;
using ISP.Application.DTOs.Plans;
using ISP.Application.DTOs.Subscribers;
using ISP.Application.DTOs.Subscriptions;
using ISP.Application.DTOs.Tenants;
using ISP.Domain.Entities;

namespace ISP.Application.Mappings
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // ============================================
            // Tenant Mappings
            // ============================================
            CreateMap<CreateTenantDto, Tenant>()
                .ForMember(dest => dest.MaxSubscribers, opt => opt.MapFrom(src =>
                    src.SubscriptionPlan == Domain.Enums.TenantPlan.Free ? 50 :
                    src.SubscriptionPlan == Domain.Enums.TenantPlan.Basic ? 500 :
                    int.MaxValue
                ));

            CreateMap<Tenant, TenantDto>()
                .ForMember(dest => dest.SubscriptionPlan, opt => opt.MapFrom(src => src.SubscriptionPlan.ToString()))
                .ForMember(dest => dest.CurrentSubscribers, opt => opt.MapFrom(src => src.Subscribers.Count))
                .ForMember(dest => dest.HasTelegramBot, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.TelegramBotToken)));


            // ============================================
            // Subscriber Mappings
            // ============================================

            CreateMap<CreateSubscriberDto, Subscriber>();

            CreateMap<Subscriber, SubscriberDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.HasTelegram, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.TelegramChatId)))
                .ForMember(dest => dest.CurrentSubscription, opt => opt.MapFrom(src =>
                    src.Subscriptions
                    .Where(s => s.Status == Domain.Enums.SubscriptionStatus.Active || s.Status == Domain.Enums.SubscriptionStatus.Expiring)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefault()
                ));

            // ============================================
            // Plan Mappings
            // ============================================
            CreateMap<CreatePlanDto, Plan>();

            CreateMap<Plan, PlanDto>()
                .ForMember(dest => dest.ActiveSubscriptionsCount, opt => opt.MapFrom(src =>
                    src.Subscriptions.Count(s => s.Status == Domain.Enums.SubscriptionStatus.Active)));


            // ============================================
            // Subscription Mappings
            // ============================================
            CreateMap<CreateSubscriptionDto, Subscription>();

            CreateMap<Subscription, SubscriptionDto>()
                .ForMember(dest => dest.SubscriberName, opt => opt.MapFrom(src => src.Subscriber.FullName))
                .ForMember(dest => dest.PlanName, opt => opt.MapFrom(src => src.Plan.Name))
                .ForMember(dest => dest.Speed, opt => opt.MapFrom(src => src.Plan.Speed))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Plan.Price))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.DaysRemaining, opt => opt.MapFrom(src =>
                    (src.EndDate - DateTime.UtcNow).Days > 0 ? (src.EndDate - DateTime.UtcNow).Days : 0));


            // ============================================
            // User Mappings
            // ============================================
            CreateMap<User, LoginResponseDto>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()))
                .ForMember(dest => dest.TenantName, opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.Name : null));
        }
    }
}