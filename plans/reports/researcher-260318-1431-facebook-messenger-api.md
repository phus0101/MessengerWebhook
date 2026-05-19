# Facebook Messenger Webhook API Research Report

**Date:** 2026-03-18
**Focus:** Webhook implementation requirements, security, and best practices

---

## 1. Webhook Verification Process

### GET Request Flow
When configuring a webhook, Facebook sends a GET request to verify your endpoint:

**Query Parameters:**
- `hub.mode` - Set to "subscribe" for subscription verification
- `hub.verify_token` - Token you define in App Dashboard for verification
- `hub.challenge` - Random string to echo back in response

**Implementation Pattern:**
```javascript
app.get('/webhook', (req, res) => {
  const mode = req.query['hub.mode'];
  const token = req.query['hub.verify_token'];
  const challenge = req.query['hub.challenge'];

  if (mode === 'subscribe' && token === VERIFY_TOKEN) {
    console.log('Webhook verified');
    res.status(200).send(challenge);
  } else {
    res.sendStatus(403);
  }
});
```

**Verification Steps:**
1. Check `hub.mode` equals "subscribe"
2. Verify `hub.verify_token` matches your configured token
3. If valid, respond with `hub.challenge` in plain text (200 status)
4. If invalid, respond with 403 Forbidden

---

## 2. Webhook Event Handling

### POST Request Structure
Facebook sends POST requests to your webhook when events occur (messages, postbacks, etc.).

**Event Types:**
- `messages` - User sends message to your Page
- `message_echoes` - Your Page sends message (echo back)
- `messaging_postbacks` - User clicks button/quick reply
- `messaging_optins` - User opts in via plugin
- `message_deliveries` - Message delivery confirmation
- `message_reads` - Message read confirmation

**Message Types:**
- **text** - Standard text messages
- **image** - Image attachments
- **audio** - Audio files
- **video** - Video files
- **file** - General file attachments
- **location** - Location data
- **reel** - Reels content
- **fallback** - URL shares creating link attachments

**Payload Structure:**
```javascript
{
  "object": "page",
  "entry": [{
    "id": "PAGE_ID",
    "time": 1234567890,
    "messaging": [{
      "sender": { "id": "USER_ID" },
      "recipient": { "id": "PAGE_ID" },
      "timestamp": 1234567890,
      "message": {
        "mid": "MESSAGE_ID",
        "text": "Hello",
        "attachments": [...]
      }
    }]
  }]
}
```

**Implementation Pattern:**
```javascript
app.post('/webhook', (req, res) => {
  const body = req.body;

  if (body.object === 'page') {
    body.entry.forEach(entry => {
      entry.messaging.forEach(event => {
        if (event.message) {
          handleMessage(event.sender.id, event.message);
        } else if (event.postback) {
          handlePostback(event.sender.id, event.postback);
        }
      });
    });
    res.status(200).send('EVENT_RECEIVED');
  } else {
    res.sendStatus(404);
  }
});
```

---

## 3. Security Requirements

### HTTPS Requirement
- **Mandatory:** Valid TLS/SSL certificate required
- **Not Supported:** Self-signed certificates rejected
- **Both Endpoints:** Verification (GET) and events (POST) must use HTTPS

### Signature Verification
Facebook includes `x-hub-signature-256` header with HMAC-SHA256 signature for payload authentication.

**Implementation Pattern:**
```javascript
const crypto = require('crypto');

function verifySignature(req, res, buf) {
  const signature = req.headers['x-hub-signature-256'];

  if (!signature) {
    throw new Error('No signature header');
  }

  const elements = signature.split('=');
  const signatureHash = elements[1];

  const expectedHash = crypto
    .createHmac('sha256', APP_SECRET)
    .update(buf)
    .digest('hex');

  if (signatureHash !== expectedHash) {
    throw new Error('Invalid signature');
  }
}

// Express middleware
app.use(express.json({ verify: verifySignature }));
```

**Security Best Practices:**
1. Always verify HMAC signature on incoming requests
2. Use timestamp validation to prevent replay attacks
3. Implement rate limiting on webhook endpoint
4. Store APP_SECRET securely (environment variables)
5. Validate payload structure before processing

---

## 4. Facebook App Setup & Permissions

### Required Permissions
- **pages_messaging** - Primary permission for Messenger Platform functionality
- **user_messenger_contact** - Required for Login Connect with Messenger

### Setup Steps
1. **Create App** - Meta for Developers dashboard
2. **Add Products** - Enable Facebook Login and Messenger
3. **Generate Token** - Graph API Explorer with pages_messaging permission
4. **Configure Webhook** - Add callback URL and verify token
5. **Subscribe to Events** - Select message, messaging_postbacks, etc.

### Development vs Production
- **Development Mode:** Send API only works for app admins/developers/testers
- **Production Mode:** Requires app review for pages_messaging permission
- **24-Hour Window:** Standard messaging window after user initiates contact
- **User Consent:** Cannot message users without their permission

---

## 5. Message Response Formats

### Send API Endpoint
```
POST https://graph.facebook.com/v18.0/me/messages
```

**Headers:**
```
Content-Type: application/json
Authorization: Bearer PAGE_ACCESS_TOKEN
```

### Text Message Response
```javascript
{
  "recipient": { "id": "USER_ID" },
  "message": { "text": "Hello, how can I help?" }
}
```

### Quick Replies
```javascript
{
  "recipient": { "id": "USER_ID" },
  "message": {
    "text": "Choose an option:",
    "quick_replies": [
      {
        "content_type": "text",
        "title": "Option 1",
        "payload": "OPTION_1"
      },
      {
        "content_type": "text",
        "title": "Option 2",
        "payload": "OPTION_2"
      }
    ]
  }
}
```

### Generic Template (Carousel)
```javascript
{
  "recipient": { "id": "USER_ID" },
  "message": {
    "attachment": {
      "type": "template",
      "payload": {
        "template_type": "generic",
        "elements": [
          {
            "title": "Product Name",
            "subtitle": "Description",
            "image_url": "https://example.com/image.jpg",
            "buttons": [
              {
                "type": "web_url",
                "url": "https://example.com",
                "title": "View Details"
              },
              {
                "type": "postback",
                "title": "Buy Now",
                "payload": "BUY_PRODUCT_123"
              }
            ]
          }
        ]
      }
    }
  }
}
```

**Template Types:**
- **Generic Template** - Structured message with image, title, subtitle, buttons
- **Button Template** - Text with up to 3 buttons
- **Receipt Template** - Order confirmation with itemized list
- **Media Template** - Image/video with optional button

**Constraints:**
- Text messages: UTF-8, max 2000 characters
- Generic template: Up to 10 elements (carousel)
- Buttons: Max 3 per template element

---

## 6. Rate Limits & Best Practices

### Rate Limits
- **General Rule:** ~1 call per second should not trigger rate limiting
- **Batch API:** Use for multiple operations to reduce call count
- **Subscription API:** Get change notifications instead of polling
- **429 Response:** "Too Many Requests" - back off and retry with exponential delay

### Best Practices
1. **Batch Requests:** Combine multiple API calls when possible
2. **Webhook Subscriptions:** Use webhooks instead of polling for updates
3. **Exponential Backoff:** Implement retry logic with increasing delays
4. **Response Time:** Respond to webhook within 20 seconds (Facebook timeout)
5. **Acknowledge Fast:** Return 200 status immediately, process async
6. **Queue Processing:** Use message queue for handling high volume

**Implementation Pattern:**
```javascript
app.post('/webhook', async (req, res) => {
  // Acknowledge immediately
  res.status(200).send('EVENT_RECEIVED');

  // Process asynchronously
  const body = req.body;
  if (body.object === 'page') {
    body.entry.forEach(entry => {
      entry.messaging.forEach(event => {
        messageQueue.add(event); // Add to queue
      });
    });
  }
});
```

---

## 7. Error Handling Recommendations

### Webhook Response Codes
- **200 OK** - Event received and processed successfully
- **403 Forbidden** - Verification failed
- **404 Not Found** - Invalid endpoint
- **500 Server Error** - Internal error (Facebook will retry)

### Send API Error Codes
- **400 Bad Request** - Invalid parameters or malformed request
- **403 Forbidden** - Permission denied or invalid token
- **429 Too Many Requests** - Rate limit exceeded
- **500 Internal Error** - Facebook server error (retry)

### Error Handling Strategy
```javascript
async function sendMessage(recipientId, message) {
  try {
    const response = await fetch(
      `https://graph.facebook.com/v18.0/me/messages?access_token=${PAGE_TOKEN}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          recipient: { id: recipientId },
          message: message
        })
      }
    );

    if (!response.ok) {
      const error = await response.json();
      console.error('Send API Error:', error);

      if (response.status === 429) {
        // Rate limited - implement exponential backoff
        await delay(5000);
        return sendMessage(recipientId, message); // Retry
      }

      throw new Error(`API Error: ${error.error.message}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Failed to send message:', error);
    // Log to monitoring service
    // Store in dead letter queue for manual review
  }
}
```

### Monitoring & Logging
1. **Log All Webhook Events** - Track incoming messages and events
2. **Monitor API Errors** - Alert on high error rates
3. **Track Response Times** - Ensure webhook responds within timeout
4. **Dead Letter Queue** - Store failed messages for retry/analysis
5. **Health Checks** - Periodic endpoint availability checks

---

## Sources

- [Meta Webhooks Getting Started Guide](https://developers.facebook.com/docs/graph-api/webhooks/getting-started/)
- [Messenger Platform Webhook Documentation](https://developers.facebook.com/docs/messenger-platform/webhook)
- [Webhook Events Reference - Messages](https://developers.facebook.com/docs/messenger-platform/reference/webhook-events/messages)
- [Message Echoes Documentation](https://developers.facebook.com/docs/messenger-platform/reference/webhook-events/message-echoes/)
- [Send API Reference](https://developers.facebook.com/docs/messenger-platform/reference/send-api/)
- [Message Templates Overview](https://developers.facebook.com/docs/messenger-platform/reference/templates/)
- [Generic Template Documentation](https://developers.facebook.com/docs/messenger-platform/send-messages/template/generic/)
- [Sending Templates Guide](https://developers.facebook.com/docs/messenger-platform/send-messages/templates/)
- [Permissions Reference](https://developers.facebook.com/docs/permissions)
- [Webhook Authentication Strategies Guide](https://www.hooklistener.com/learn/webhook-authentication-strategies)
- [Webhook Security Fundamentals](https://www.hooklistener.com/learn/webhook-security-fundamentals)
