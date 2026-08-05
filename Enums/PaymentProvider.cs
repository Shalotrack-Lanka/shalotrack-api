namespace ShaloTrack_API.Enums;

// Only Manual exists today (no payment gateway merchant account yet).
// Adding a real gateway later means adding a new value here plus a new
// IPaymentProvider implementation -- not changing anything that already
// depends on IPaymentProvider.
public enum PaymentProvider
{
    Manual,
    PayHere
}