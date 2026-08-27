// StoreKit seam for the Full Collection unlock.
//
// The managed side calls _DreidelIapAvailable() first and shows an honest "not available
// here" when it answers 0, so this file compiling to a no-op is a supported state rather
// than a broken one. Wire the three functions to StoreKit when the product exists in App
// Store Connect; nothing above this file needs to change.
//
// On success call back into Unity exactly as the Android wrapper does:
//     UnitySendMessage("Bootstrap", "OnPurchaseComplete", "");   // a fresh purchase
//     UnitySendMessage("Bootstrap", "OnPurchaseRestored", "");   // a replayed one
#import <Foundation/Foundation.h>

extern "C" {

// Flip to 1 once the StoreKit calls below are real. Returning 0 keeps the button honest:
// the game says purchases aren't available in this build instead of pretending to sell.
int _DreidelIapAvailable(void)
{
    return 0;
}

void _DreidelIapBuyFullCollection(void)
{
    // SKPaymentQueue / StoreKit 2 purchase for the Full Collection product id goes here.
}

void _DreidelIapRestore(void)
{
    // restoreCompletedTransactions (or Transaction.currentEntitlements) goes here.
}

}
