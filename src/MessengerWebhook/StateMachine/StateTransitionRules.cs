using MessengerWebhook.Data.Entities;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.StateMachine;

public static class StateTransitionRules
{
    private static readonly List<StateTransitionRule> _rules = new()
    {
        // From Idle
        new() { FromState = ConversationState.Idle, ToState = ConversationState.Greeting },

        // From Greeting
        new() { FromState = ConversationState.Greeting, ToState = ConversationState.MainMenu },
        new() { FromState = ConversationState.Greeting, ToState = ConversationState.SkinConsultation },
        new() { FromState = ConversationState.Greeting, ToState = ConversationState.OrderTracking },

        // From MainMenu
        new() { FromState = ConversationState.MainMenu, ToState = ConversationState.BrowsingProducts },
        new() { FromState = ConversationState.MainMenu, ToState = ConversationState.SkinConsultation },
        new() { FromState = ConversationState.MainMenu, ToState = ConversationState.OrderTracking },
        new() { FromState = ConversationState.MainMenu, ToState = ConversationState.Help },

        // From BrowsingProducts
        new() { FromState = ConversationState.BrowsingProducts, ToState = ConversationState.ProductDetail },
        new() { FromState = ConversationState.BrowsingProducts, ToState = ConversationState.CartReview,
            Condition = ctx => ctx.GetData<List<string>>("cartItems")?.Count > 0 },
        new() { FromState = ConversationState.BrowsingProducts, ToState = ConversationState.MainMenu },

        // From ProductDetail
        new() { FromState = ConversationState.ProductDetail, ToState = ConversationState.SkinAnalysis },
        new() { FromState = ConversationState.ProductDetail, ToState = ConversationState.VariantSelection },
        new() { FromState = ConversationState.ProductDetail, ToState = ConversationState.BrowsingProducts },

        // From SkinAnalysis
        new() { FromState = ConversationState.SkinAnalysis, ToState = ConversationState.BrowsingProducts },

        // From VariantSelection
        new() { FromState = ConversationState.VariantSelection, ToState = ConversationState.AddToCart },
        new() { FromState = ConversationState.VariantSelection, ToState = ConversationState.ProductDetail },

        // From AddToCart
        new() { FromState = ConversationState.AddToCart, ToState = ConversationState.CartReview },
        new() { FromState = ConversationState.AddToCart, ToState = ConversationState.BrowsingProducts },

        // From CartReview
        new() { FromState = ConversationState.CartReview, ToState = ConversationState.ShippingAddress,
            Condition = ctx => ctx.GetData<List<string>>("cartItems")?.Count > 0 },
        new() { FromState = ConversationState.CartReview, ToState = ConversationState.BrowsingProducts },

        // From ShippingAddress
        new() { FromState = ConversationState.ShippingAddress, ToState = ConversationState.PaymentMethod },
        new() { FromState = ConversationState.ShippingAddress, ToState = ConversationState.CartReview },

        // From PaymentMethod
        new() { FromState = ConversationState.PaymentMethod, ToState = ConversationState.OrderConfirmation },
        new() { FromState = ConversationState.PaymentMethod, ToState = ConversationState.ShippingAddress },

        // From OrderConfirmation
        new() { FromState = ConversationState.OrderConfirmation, ToState = ConversationState.OrderPlaced },

        // From OrderPlaced
        new() { FromState = ConversationState.OrderPlaced, ToState = ConversationState.OrderTracking },
        new() { FromState = ConversationState.OrderPlaced, ToState = ConversationState.MainMenu },

        // From OrderTracking
        new() { FromState = ConversationState.OrderTracking, ToState = ConversationState.MainMenu },

        // From SkinConsultation
        new() { FromState = ConversationState.SkinConsultation, ToState = ConversationState.BrowsingProducts },
        new() { FromState = ConversationState.SkinConsultation, ToState = ConversationState.MainMenu },

        // From Help (can return to any state)
        new() { FromState = ConversationState.Help, ToState = ConversationState.Idle },
        new() { FromState = ConversationState.Help, ToState = ConversationState.MainMenu },
        new() { FromState = ConversationState.Help, ToState = ConversationState.BrowsingProducts },
        new() { FromState = ConversationState.Help, ToState = ConversationState.CartReview },

        // From Error
        new() { FromState = ConversationState.Error, ToState = ConversationState.Idle },

        // To Help (from any state)
        new() { FromState = ConversationState.Idle, ToState = ConversationState.Help },
        new() { FromState = ConversationState.Greeting, ToState = ConversationState.Help },
        new() { FromState = ConversationState.BrowsingProducts, ToState = ConversationState.Help },
        new() { FromState = ConversationState.ProductDetail, ToState = ConversationState.Help },
        new() { FromState = ConversationState.SkinAnalysis, ToState = ConversationState.Help },
        new() { FromState = ConversationState.VariantSelection, ToState = ConversationState.Help },
        new() { FromState = ConversationState.AddToCart, ToState = ConversationState.Help },
        new() { FromState = ConversationState.CartReview, ToState = ConversationState.Help },
        new() { FromState = ConversationState.ShippingAddress, ToState = ConversationState.Help },
        new() { FromState = ConversationState.PaymentMethod, ToState = ConversationState.Help },
        new() { FromState = ConversationState.OrderConfirmation, ToState = ConversationState.Help },
        new() { FromState = ConversationState.OrderPlaced, ToState = ConversationState.Help },
        new() { FromState = ConversationState.OrderTracking, ToState = ConversationState.Help },
        new() { FromState = ConversationState.SkinConsultation, ToState = ConversationState.Help },

        // To Error (from any state)
        new() { FromState = ConversationState.Idle, ToState = ConversationState.Error },
        new() { FromState = ConversationState.Greeting, ToState = ConversationState.Error },
        new() { FromState = ConversationState.MainMenu, ToState = ConversationState.Error },
        new() { FromState = ConversationState.BrowsingProducts, ToState = ConversationState.Error },
        new() { FromState = ConversationState.ProductDetail, ToState = ConversationState.Error },
        new() { FromState = ConversationState.SkinAnalysis, ToState = ConversationState.Error },
        new() { FromState = ConversationState.VariantSelection, ToState = ConversationState.Error },
        new() { FromState = ConversationState.AddToCart, ToState = ConversationState.Error },
        new() { FromState = ConversationState.CartReview, ToState = ConversationState.Error },
        new() { FromState = ConversationState.ShippingAddress, ToState = ConversationState.Error },
        new() { FromState = ConversationState.PaymentMethod, ToState = ConversationState.Error },
        new() { FromState = ConversationState.OrderConfirmation, ToState = ConversationState.Error },
        new() { FromState = ConversationState.OrderPlaced, ToState = ConversationState.Error },
        new() { FromState = ConversationState.OrderTracking, ToState = ConversationState.Error },
        new() { FromState = ConversationState.SkinConsultation, ToState = ConversationState.Error }
    };

    public static bool IsValidTransition(ConversationState fromState, ConversationState toState, StateContext context)
    {
        var rule = _rules.FirstOrDefault(r => r.FromState == fromState && r.ToState == toState);
        return rule?.CanTransition(context) ?? false;
    }

    public static IEnumerable<ConversationState> GetAllowedTransitions(ConversationState fromState, StateContext context)
    {
        return _rules
            .Where(r => r.FromState == fromState && r.CanTransition(context))
            .Select(r => r.ToState)
            .Distinct();
    }
}
