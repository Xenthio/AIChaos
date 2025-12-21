# Stripe Payment Integration Setup Guide

This guide walks you through setting up Stripe for on-site credit purchases, eliminating the need for users to manually enter usernames during payment.

---

## 📋 Overview

**Why Stripe for On-Site Payments?**
- ✅ **No username errors** - Users are already logged in, we know who's paying
- ✅ **Professional** - Industry-standard payment processor
- ✅ **Low fees** - 2.9% + $0.30 per transaction (vs YouTube's ~30%)
- ✅ **On-site experience** - Users stay on your website
- ✅ **Instant credits** - Automated via webhooks
- ✅ **Secure** - PCI compliance handled by Stripe

**How it works:**
1. User logs into AIChaos
2. User clicks "Add Credits" button
3. Stripe Checkout opens (hosted by Stripe)
4. User completes payment
5. Stripe webhook confirms payment
6. Credits auto-added to user's account (no username needed!)

---

## 🚀 Quick Setup (15 Minutes)

### Step 1: Create Stripe Account

1. Go to [stripe.com](https://stripe.com/)
2. Click **"Sign Up"**
3. Complete business verification
   - Business name
   - Tax ID (EIN or SSN)
   - Bank account details
4. Verify your email

> **Note:** Verification can take a few hours to a few days depending on your region.

### Step 2: Get API Keys

1. Log into Stripe Dashboard
2. Click **"Developers"** in left sidebar
3. Click **"API keys"**
4. You'll see two types of keys:
   - **Publishable key** (starts with `pk_test_` or `pk_live_`)
   - **Secret key** (starts with `sk_test_` or `sk_live_`)
5. Copy both keys (start with test keys for development)

> **Security:** Never share your secret key or commit it to source control!

### Step 3: Configure Webhook

1. In Stripe Dashboard, go to **Developers** → **Webhooks**
2. Click **"Add endpoint"**
3. Enter your webhook URL: `https://your-domain.com/api/payments/stripe/webhook`
4. Under **"Events to send"**, select:
   - `checkout.session.completed`
   - `checkout.session.expired`
5. Click **"Add endpoint"**
6. Copy the **Signing secret** (starts with `whsec_`)

> **Important:** Your webhook URL must use HTTPS in production. Use ngrok for local testing.

### Step 4: Configure AIChaos

1. Open AIChaos Dashboard → **Setup** tab
2. Scroll to **"Payment Providers"** section
3. Under **"Stripe Integration"**:
   - Check **"Enable Stripe"**
   - Paste **Publishable Key**
   - Paste **Secret Key**
   - Paste **Webhook Secret**
   - Set **Minimum Payment Amount** (default $1.00)
4. Click **"Save Settings"**

### Step 5: Test It!

1. Make sure you're using **test keys** (pk_test_... and sk_test_...)
2. Log into AIChaos as a regular user
3. Click **"Add Credits"** button
4. Select an amount (e.g., $5.00)
5. Click **"Add Credits via Stripe"**
6. Use Stripe test card: `4242 4242 4242 4242`
   - Expiry: Any future date
   - CVC: Any 3 digits
   - ZIP: Any 5 digits
7. Complete payment
8. Verify credits added to your account

🎉 **Done!** Your Stripe integration is ready!

---

## 🔧 Configuration Details

### Settings File (settings.json)

```json
{
  "AIChaos": {
    "PaymentProviders": {
      "Stripe": {
        "Enabled": true,
        "PublishableKey": "pk_test_xxxxx",
        "SecretKey": "sk_test_xxxxx",
        "WebhookSecret": "whsec_xxxxx",
        "MinPaymentAmount": 1.00
      }
    }
  }
}
```

### Environment Variables (Recommended for Production)

For better security, use environment variables:

```bash
export STRIPE_SECRET_KEY="sk_live_xxxxx"
export STRIPE_WEBHOOK_SECRET="whsec_xxxxx"
```

Update your configuration to read from environment:
```json
{
  "Stripe": {
    "SecretKey": "${STRIPE_SECRET_KEY}",
    "WebhookSecret": "${STRIPE_WEBHOOK_SECRET}"
  }
}
```

---

## 🎨 User Experience

### What Users See

1. **Add Credits Button**
   - Shows on dashboard/profile
   - Clear call-to-action

2. **Amount Selection**
   - Predefined amounts ($5, $10, $20, $50)
   - Custom amount option
   - Shows how many Ideas each amount equals

3. **Stripe Checkout**
   - Professional payment form (hosted by Stripe)
   - Supports credit cards, Apple Pay, Google Pay
   - Mobile-optimized
   - Multi-currency support

4. **Success Page**
   - Credits instantly added
   - Confirmation message
   - Return to dashboard

### No Username Entry Required!

Unlike Ko-fi, users **don't need to remember** to include their username:
- User is already logged in
- We know their account ID from session
- Payment is automatically linked to their account
- Zero chance of user error

---

## 🔒 Security Features

### Implemented Security

1. **Webhook Signature Verification**
   - Every webhook is verified using Stripe's signature
   - Prevents fake payment notifications
   - Rejects tampered data

2. **HTTPS Required**
   - Stripe requires HTTPS for production webhooks
   - Protects data in transit

3. **PCI Compliance**
   - Stripe handles all credit card data
   - Your server never sees card numbers
   - Reduces security liability

4. **Duplicate Prevention**
   - Session IDs tracked to prevent double-crediting
   - Idempotent webhook processing

5. **Session Metadata**
   - Account ID stored in checkout session
   - Links payment to correct user
   - Tamper-proof (verified by Stripe)

### Best Practices

1. **Use environment variables** for API keys
2. **Never commit** secrets to git
3. **Rotate keys** periodically
4. **Monitor** webhook failures
5. **Test** with test keys first
6. **Enable** Stripe Radar for fraud prevention

---

## 🐛 Troubleshooting

### "Webhook signature verification failed"

**Problem:** Stripe webhook returns 400 error
**Solution:**
1. Check webhook secret is correct in settings
2. Verify HTTPS is working (required for production)
3. Ensure webhook URL matches exactly
4. Check Stripe Dashboard → Webhooks → Recent events for errors

### "Payment succeeded but credits not added"

**Problem:** Payment works but credits don't appear
**Solution:**
1. Check server logs for webhook processing errors
2. Verify webhook events are being sent (Stripe Dashboard)
3. Check account ID in session is correct
4. Test with Stripe CLI: `stripe listen --forward-to localhost:5000/api/payments/stripe/webhook`

### "Checkout session creation failed"

**Problem:** Can't create payment session
**Solution:**
1. Verify API keys are correct (secret key, not publishable)
2. Check Stripe account is fully verified
3. Ensure user is logged in (has valid session)
4. Check minimum amount requirements

### "Card declined"

**Problem:** Test card doesn't work
**Solution:**
- Use correct test card: `4242 4242 4242 4242`
- Any future expiry date
- Any 3-digit CVC
- Any 5-digit ZIP
- See [Stripe test cards](https://stripe.com/docs/testing) for more

---

## 📊 Monitoring

### Stripe Dashboard

Monitor your payments:
1. **Home** - Overview of recent payments
2. **Payments** - Detailed transaction list
3. **Customers** - Customer data (optional)
4. **Webhooks** - Webhook delivery status
5. **Logs** - API request logs

### AIChaos Monitoring

Check integration status:
```
GET /api/payments/stripe/status
```

Response:
```json
{
  "enabled": true,
  "processed_payments": 42,
  "webhook_url": "https://your-domain.com/api/payments/stripe/webhook"
}
```

### Server Logs

Stripe events are logged with `[Stripe]` prefix:
```
[Stripe] Created checkout session sess_123 for JohnDoe ($5.00)
[Stripe] Received webhook event: checkout.session.completed
[Stripe] Added $5.00 credits to JohnDoe from session sess_123
```

---

## 💰 Pricing & Fees

### Stripe Fees

**Standard Pricing:**
- 2.9% + $0.30 per successful card charge
- No monthly fees
- No setup fees
- No hidden fees

**Examples:**
| Donation | Stripe Fee | You Receive | Effective Rate |
|----------|------------|-------------|----------------|
| $1.00 | $0.33 | $0.67 | 33% |
| $5.00 | $0.45 | $4.55 | 9% |
| $10.00 | $0.59 | $9.41 | 5.9% |
| $50.00 | $1.76 | $48.24 | 3.5% |

> **Tip:** Encourage larger donations to reduce effective fee percentage!

### Comparison to YouTube

| Amount | YouTube (~30% fee) | Stripe (2.9% + $0.30) | Savings |
|--------|-------------------|----------------------|---------|
| $10 | $7.00 | $9.41 | **+$2.41 (+34%)** |
| $50 | $35.00 | $48.24 | **+$13.24 (+38%)** |
| $100 | $70.00 | $97.11 | **+$27.11 (+39%)** |

---

## 🔄 Going Live (Production)

### Checklist

Before accepting real payments:

- [ ] Stripe account fully verified
- [ ] Using **live keys** (pk_live_... and sk_live_...)
- [ ] Webhook configured with live endpoint URL
- [ ] HTTPS enabled on your domain
- [ ] Test end-to-end with real (small) payment
- [ ] Monitoring and alerts set up
- [ ] Backup webhook endpoint configured (optional)
- [ ] Terms of service updated to mention payments
- [ ] Privacy policy mentions Stripe

### Switching from Test to Live

1. **Get live API keys**
   - Stripe Dashboard → Developers → API keys
   - Toggle from "Test mode" to "Live mode"
   - Copy live keys (pk_live_... and sk_live_...)

2. **Update AIChaos settings**
   - Replace test keys with live keys
   - Update webhook secret (live webhooks have different secret)

3. **Update Stripe webhook**
   - Create new webhook endpoint for live mode
   - Use same URL but ensure it's production URL
   - Copy new webhook secret

4. **Test with small payment**
   - Use real card with small amount ($1)
   - Verify credits added correctly
   - Check webhook delivery in Stripe Dashboard

5. **Monitor closely**
   - Watch first few payments carefully
   - Check logs for any errors
   - Verify all webhooks are processing

---

## 🎯 Comparison: Stripe vs Ko-fi

| Feature | Stripe (On-Site) | Ko-fi (Off-Site) |
|---------|------------------|------------------|
| **Username Entry** | ❌ Not needed (auto-detected) | ⚠️ Required (user must type) |
| **Platform Fee** | 2.9% + $0.30 | 0% (but ~3% processor) |
| **User Experience** | ✅ Stays on your site | ❌ Leaves to Ko-fi |
| **Setup Complexity** | ⚠️ Medium | ✅ Easy |
| **Payment Methods** | Cards, Apple Pay, Google Pay | Cards, PayPal, Apple Pay |
| **Error Risk** | ✅ None (automated) | ⚠️ Medium (typos) |
| **Verification Time** | ⚠️ 1-3 days | ✅ Instant |

**Recommendation:**
- **Use Stripe** if you want professional on-site payments with zero user error
- **Use Ko-fi** if you want quickest setup and don't mind users leaving site
- **Use both!** Offer multiple payment options for user preference

---

## 📚 Additional Resources

### Official Documentation
- [Stripe Checkout Documentation](https://stripe.com/docs/payments/checkout)
- [Stripe Webhooks Guide](https://stripe.com/docs/webhooks)
- [Stripe Testing Guide](https://stripe.com/docs/testing)
- [Stripe .NET Library](https://github.com/stripe/stripe-dotnet)

### Stripe Tools
- [Stripe CLI](https://stripe.com/docs/stripe-cli) - Test webhooks locally
- [Stripe Dashboard](https://dashboard.stripe.com/) - Monitor payments
- [Stripe Sigma](https://stripe.com/sigma) - Analytics (paid feature)

### AIChaos Documentation
- [Payment Options Comparison](PAYMENT_OPTIONS.md)
- [Ko-fi Setup Guide](KOFI_SETUP.md)
- [Implementation Summary](IMPLEMENTATION_SUMMARY.md)

---

## ❓ FAQ

**Q: Do I need a business to use Stripe?**
A: Yes, Stripe requires business verification (can be sole proprietorship with SSN).

**Q: What currencies does Stripe support?**
A: 135+ currencies. Automatic conversion to your account currency.

**Q: Can users save cards for future payments?**
A: Yes, with Stripe Customer Portal (optional feature).

**Q: What if a payment is disputed/chargebacked?**
A: Stripe handles disputes. You can respond via dashboard. Fee: $15 per dispute.

**Q: How fast are credits added?**
A: Instant - within seconds of payment completion via webhook.

**Q: Can I refund payments?**
A: Yes, via Stripe Dashboard or programmatically. Should manually deduct credits.

**Q: Is Stripe available in my country?**
A: Check [Stripe's country list](https://stripe.com/global). Available in 40+ countries.

**Q: What about recurring subscriptions?**
A: Supported! Can add subscription tiers for monthly credit packages.

**Q: HTTPS required everywhere?**
A: Only for production webhooks. Use ngrok for local/test development.

---

## 🎬 Next Steps

After Stripe is set up:

1. **Test thoroughly** with test cards
2. **Add UI button** to dashboard/profile page
3. **Update stream graphics** with payment options
4. **Announce to viewers** about new payment method
5. **Monitor** first few transactions closely
6. **Consider** offering both Stripe and Ko-fi for user choice

Need help? Check the [main README](README.md) or open an issue on GitHub.

---

**Last Updated:** December 2024  
**Version:** 1.0  
**Compatible with:** AIChaos v1.0+

