# Ko-fi Payment Integration Setup Guide

This guide walks you through setting up Ko-fi as a payment method for AIChaos, allowing viewers to donate and receive credits with **0% platform fees** (only standard payment processor fees apply).

---

## 📋 Overview

**Why Ko-fi?**
- ✅ **0% platform fees** - Ko-fi takes 0% cut
- ✅ **Simple setup** - Just webhook configuration
- ✅ **No monthly costs** - Free tier is sufficient
- ✅ **Lower total fees** - ~3% vs YouTube's ~30%
- ✅ **Multiple payment methods** - Credit card, PayPal, Apple Pay, Google Pay

**How it works:**
1. Viewer visits your Ko-fi page (e.g., `ko-fi.com/yourname`)
2. Viewer donates with message like `username: JohnDoe`
3. Ko-fi sends webhook to your AIChaos server
4. Credits automatically added to user's account
5. User can submit Ideas immediately

---

## 🚀 Quick Setup (5 Minutes)

### Step 1: Create Ko-fi Account

1. Go to [ko-fi.com](https://ko-fi.com/)
2. Click **"Sign Up"** (it's free)
3. Choose your Ko-fi page name (e.g., `ko-fi.com/yourname`)
4. Complete your profile

### Step 2: Get Webhook Settings

1. Log into Ko-fi
2. Go to your Ko-fi Settings
3. Navigate to **"API"** section (in left sidebar under "Account")
4. Scroll to **"Webhooks"**
5. Copy your **Verification Token** (you'll need this)

> **Note:** Webhook URL will be configured in Step 4 after you have your public URL.

### Step 3: Configure AIChaos

1. Start your AIChaos server if not already running
2. Open **http://localhost:5000/dashboard**
3. Go to **Setup** tab
4. Scroll to **"Payment Providers"** section
5. Under **"Ko-fi Integration"**:
   - Check **"Enable Ko-fi"**
   - Paste your **Verification Token**
   - Set **Minimum Donation Amount** (default $1.00)
6. Click **"Save Settings"**

### Step 4: Set Up Webhook URL

You need a public URL for Ko-fi to send webhooks to. You have two options:

#### Option A: Using ngrok (Recommended for Testing)

1. Install ngrok: [ngrok.com/download](https://ngrok.com/download)
2. Run: `ngrok http 5000`
3. Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)
4. Your webhook URL will be: `https://abc123.ngrok.io/api/payments/kofi`

#### Option B: Using Your Own Domain

If you have a domain pointing to your server:
- Your webhook URL will be: `https://yourdomain.com/api/payments/kofi`
- Ensure HTTPS is set up (Let's Encrypt recommended)

### Step 5: Register Webhook with Ko-fi

1. Back in Ko-fi Settings → API → Webhooks
2. Click **"Add Webhook URL"**
3. Enter your webhook URL from Step 4
4. Click **"Update"**
5. Ko-fi will send a test webhook to verify

> **Important:** Ko-fi requires HTTPS for webhooks in production. Use ngrok for testing.

### Step 6: Test It!

1. Make a test donation to your own Ko-fi page
2. In the donation message, include: `username: yourusername`
3. Check AIChaos logs - you should see:
   ```
   [Ko-fi] Received webhook
   [Ko-fi] Added {amount} credits to {username}
   ```
4. Check your account balance in AIChaos

🎉 **Done!** Your Ko-fi integration is ready!

---

## 📝 User Instructions (Share with Viewers)

### For Viewers to Get Credits

**Step 1: Create AIChaos Account**
1. Go to `[your-aichaos-url]`
2. Click "Create Account"
3. Choose a username (remember this!)
4. Set a password

**Step 2: Donate via Ko-fi**
1. Visit `ko-fi.com/[your-kofi-name]`
2. Click "Support" or "Donate"
3. Enter donation amount (minimum $1)
4. **Important:** In the message field, type:
   ```
   username: YourUsername
   ```
   (Replace `YourUsername` with your AIChaos username)
5. Complete payment

**Step 3: Credits Added Automatically**
- Credits appear in your account within seconds
- Refresh the AIChaos page if needed
- Check your balance in top-right corner
- Start submitting Ideas!

### Supported Username Formats

The system recognizes several username formats in the donation message:
- `username: JohnDoe`
- `user: JohnDoe`
- `for: JohnDoe`
- `account: JohnDoe`
- `@JohnDoe`
- Just `JohnDoe` (if it's the first word and looks like a username)

> **Tip:** Tell viewers to put their username at the beginning of the message for best results.

---

## 🔧 Advanced Configuration

### Minimum Donation Amount

By default, the minimum donation is $1.00. You can change this:

```json
{
  "AIChaos": {
    "PaymentProviders": {
      "Kofi": {
        "MinDonationAmount": 1.00
      }
    }
  }
}
```

### Multiple Payment Methods

Ko-fi is designed to work alongside other payment methods:
- **YouTube Super Chat** - For viewers who prefer staying on YouTube
- **Ko-fi** - For viewers who want lower fees
- **Stripe** - For direct on-site payments (coming soon)

All methods credit the same account system.

---

## 🛡️ Security Notes

### Verification Token

- **Never share** your verification token publicly
- Store it securely in `settings.json`
- Rotate it if compromised (Ko-fi Settings → API)

### Webhook Security

The Ko-fi integration includes:
- ✅ Verification token validation
- ✅ Duplicate transaction prevention
- ✅ Amount validation
- ✅ HTTPS requirement (production)

### Transaction IDs

Each donation is tracked by unique transaction ID to prevent:
- Duplicate processing
- Replay attacks
- Accidental double-credits

---

## 🐛 Troubleshooting

### "Webhook verification failed"

**Problem:** Ko-fi webhook returns error
**Solution:**
1. Check verification token is correct in settings
2. Ensure Ko-fi integration is enabled
3. Check server logs for specific error
4. Verify webhook URL is correct

### "Username not found"

**Problem:** Credits not added to account
**Solution:**
- Username must exist before donation
- Check username format in donation message
- Credits are held as "pending" until account created
- Admin can manually link pending credits

### "Credits not appearing"

**Problem:** Donation processed but credits missing
**Solution:**
1. Check server logs: `grep "Ko-fi" logs.txt`
2. Verify donation amount met minimum ($1.00)
3. Check if username was included in message
4. View pending credits: GET `/api/payments/pending`
5. Manually link credits if needed

### "Webhook not receiving data"

**Problem:** Ko-fi sending but server not receiving
**Solution:**
1. Verify webhook URL is correct
2. Check HTTPS is working (ngrok or domain)
3. Ensure port 5000 is accessible
4. Check firewall settings
5. Test with Ko-fi's webhook test button

---

## 📊 Monitoring

### View Ko-fi Status

GET `/api/payments/kofi/status`

Response:
```json
{
  "enabled": true,
  "processed_transactions": 42,
  "webhook_url": "https://your-url.com/api/payments/kofi"
}
```

### View Pending Credits

GET `/api/payments/pending`

Shows donations waiting to be linked to accounts.

### Server Logs

Ko-fi events are logged with `[Ko-fi]` prefix:
```
[Ko-fi] Received webhook
[Ko-fi] Type: Donation, From: JohnDoe, Amount: 5.00 USD
[Ko-fi] Added 5.00 credits to JohnDoe
```

---

## 💡 Best Practices

### 1. Clear Instructions

Provide clear donation instructions on your stream:
- Show your Ko-fi link on screen
- Explain username format requirement
- Display example: `username: YourName`

### 2. Test First

- Test with small donation ($1)
- Verify credits appear correctly
- Check logs for any errors

### 3. Backup Methods

- Keep YouTube Super Chat enabled as backup
- Some viewers prefer staying on YouTube
- Ko-fi works great as primary low-fee option

### 4. Monitor Pending Credits

- Check pending credits dashboard regularly
- Help users who forgot to include username
- Manually link credits when needed

---

## 🔄 Migration from YouTube-Only

### Current State
- YouTube Super Chat: $1 donation → ~$0.70 to you (~30% fees)

### After Ko-fi Setup
- Ko-fi: $1 donation → ~$0.97 to you (~3% fees)
- **38% more revenue per donation!**

### Transition Strategy

1. **Add Ko-fi** as additional payment method
2. **Announce** to viewers in stream/Discord
3. **Show** Ko-fi link on stream overlay
4. **Educate** viewers about lower fees
5. **Keep** YouTube as option for convenience

Users can choose their preferred payment method!

---

## 📚 Additional Resources

### Ko-fi Documentation
- [Ko-fi Webhook Guide](https://ko-fi.com/manage/webhooks)
- [Ko-fi API Reference](https://ko-fi.com/manage/webhooks?src=sidemenu)
- [Ko-fi Support](https://help.ko-fi.com/)

### AIChaos Documentation
- [Payment Options Comparison](PAYMENT_OPTIONS.md)
- [YouTube Setup Guide](YOUTUBE_SETUP.md)
- [Main README](README.md)

---

## ❓ FAQ

**Q: Does Ko-fi take any fees?**
A: Ko-fi platform fee is 0%. You only pay standard payment processor fees (~3%).

**Q: Can viewers donate without creating an account?**
A: Yes, donations are held as "pending credits" until account is created. Credits are added automatically when user registers with matching username.

**Q: What if viewer forgets to include username?**
A: Donation is held as pending. Admin can manually link credits to correct account.

**Q: Can I use Ko-fi and YouTube together?**
A: Yes! Both methods work simultaneously. Users choose their preferred method.

**Q: Is HTTPS required?**
A: Yes, for production. Use ngrok for local testing.

**Q: What currencies does Ko-fi support?**
A: Ko-fi supports USD, EUR, GBP, and many others. Credits are always converted to USD.

**Q: How fast are credits added?**
A: Instant - webhooks process in seconds after donation.

**Q: Can I refund donations?**
A: Yes, through Ko-fi dashboard. Credits should be manually deducted in AIChaos.

---

## 🎬 Next Steps

After Ko-fi is set up:

1. **Test thoroughly** with small donations
2. **Update stream graphics** with Ko-fi link
3. **Announce to viewers** in stream/Discord
4. **Monitor** for first few days
5. **Consider** adding more payment options (Stripe, PayPal)

Need help? Check the [Payment Options Guide](PAYMENT_OPTIONS.md) or open an issue on GitHub.

---

**Last Updated:** December 2024  
**Version:** 1.0  
**Compatible with:** AIChaos v1.0+

