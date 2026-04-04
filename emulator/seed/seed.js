const { ServiceBusClient } = require('@azure/service-bus');

const CONNECTION_STRING = process.env.CONNECTION_STRING ||
  'Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;';

async function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

async function withRetry(label, fn, maxRetries = 20, delayMs = 4000) {
  for (let i = 0; i < maxRetries; i++) {
    try { return await fn(); }
    catch (err) {
      console.log(`[${label}] attempt ${i + 1}/${maxRetries} failed: ${err.message}`);
      if (i < maxRetries - 1) await sleep(delayMs);
      else throw err;
    }
  }
}

async function sendToQueue(client, queue, messages) {
  const sender = client.createSender(queue);
  await sender.sendMessages(messages);
  await sender.close();
}

async function dlqFromQueue(client, queue, count, reason, description) {
  const receiver = client.createReceiver(queue, { receiveMode: 'peekLock' });
  const msgs = await receiver.receiveMessages(count, { maxWaitTimeInMs: 8000 });
  for (const m of msgs)
    await receiver.deadLetterMessage(m, { deadLetterReason: reason, deadLetterErrorDescription: description });
  await receiver.close();
}

async function dlqFromSubscription(client, topic, sub, count, reason, description) {
  const receiver = client.createReceiver(topic, sub, { receiveMode: 'peekLock' });
  const msgs = await receiver.receiveMessages(count, { maxWaitTimeInMs: 8000 });
  for (const m of msgs)
    await receiver.deadLetterMessage(m, { deadLetterReason: reason, deadLetterErrorDescription: description });
  await receiver.close();
}

// ── Seed data ──────────────────────────────────────────────────────────────────

const ordersDlq = [
  { messageId: 'msg-ord-001', subject: 'OrderReceived', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10041', customerId: 'C-8812', amount: 0.00, items: [], source: 'web' }),
    applicationProperties: { region: 'eu-west', version: '2' } },
  { messageId: 'msg-ord-002', subject: 'OrderReceived', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10042', customerId: 'C-0023', amount: -19.99, items: ['SKU-441'], source: 'mobile' }),
    applicationProperties: { region: 'eu-west', version: '2' } },
  { messageId: 'msg-ord-003', subject: 'OrderReceived', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10043', customerId: null, amount: 89.50, items: ['SKU-119', 'SKU-220'], source: 'api' }),
    applicationProperties: { region: 'us-east', version: '1' } },
];

const ordersActive = [
  { messageId: 'msg-ord-004', subject: 'OrderReceived', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10044', customerId: 'C-3391', amount: 349.95, items: ['SKU-780', 'SKU-781'], source: 'web' }),
    applicationProperties: { region: 'eu-west', version: '2' } },
  { messageId: 'msg-ord-005', subject: 'OrderReceived', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10045', customerId: 'C-7742', amount: 59.00, items: ['SKU-330'], source: 'mobile' }),
    applicationProperties: { region: 'us-east', version: '2' } },
  { messageId: 'msg-ord-006', subject: 'OrderReceived', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10046', customerId: 'C-1105', amount: 1299.00, items: ['SKU-999'], source: 'web' }),
    applicationProperties: { region: 'eu-central', version: '2' } },
  { messageId: 'msg-ord-007', subject: 'OrderReceived', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10047', customerId: 'C-5566', amount: 24.99, items: ['SKU-002', 'SKU-003'], source: 'api' }),
    applicationProperties: { region: 'eu-west', version: '2' } },
];

const paymentsDlq = [
  { messageId: 'msg-pay-001', subject: 'PaymentInitiated', contentType: 'application/json',
    body: JSON.stringify({ paymentId: 'PAY-88231', orderId: 'ORD-2024-09871', amount: 149.99, provider: 'stripe', cardLast4: '4242' }),
    applicationProperties: { retryCount: '3', provider: 'stripe' } },
  { messageId: 'msg-pay-002', subject: 'PaymentInitiated', contentType: 'application/json',
    body: JSON.stringify({ paymentId: 'PAY-88232', orderId: 'ORD-2024-09872', amount: 79.00, provider: 'adyen', cardLast4: '0000' }),
    applicationProperties: { retryCount: '3', provider: 'adyen' } },
  { messageId: 'msg-pay-003', subject: 'PaymentInitiated', contentType: 'application/json',
    body: JSON.stringify({ paymentId: 'PAY-88233', orderId: 'ORD-2024-09873', amount: 599.95, provider: 'stripe', cardLast4: '1234' }),
    applicationProperties: { retryCount: '3', provider: 'stripe' } },
  { messageId: 'msg-pay-004', subject: 'RefundRequested', contentType: 'application/json',
    body: JSON.stringify({ refundId: 'REF-1021', orderId: 'ORD-2024-09750', amount: 29.99, reason: 'duplicate_charge' }),
    applicationProperties: { retryCount: '3', provider: 'stripe' } },
];

const paymentsActive = [
  { messageId: 'msg-pay-005', subject: 'PaymentInitiated', contentType: 'application/json',
    body: JSON.stringify({ paymentId: 'PAY-88240', orderId: 'ORD-2024-10044', amount: 349.95, provider: 'stripe', cardLast4: '4000' }),
    applicationProperties: { retryCount: '0', provider: 'stripe' } },
  { messageId: 'msg-pay-006', subject: 'PaymentInitiated', contentType: 'application/json',
    body: JSON.stringify({ paymentId: 'PAY-88241', orderId: 'ORD-2024-10045', amount: 59.00, provider: 'paypal', cardLast4: null }),
    applicationProperties: { retryCount: '0', provider: 'paypal' } },
];

const emailsDlq = [
  { messageId: 'msg-eml-001', subject: 'SendOrderConfirmation', contentType: 'application/json',
    body: JSON.stringify({ to: 'user@example.com', templateId: 'order-confirm', orderId: 'ORD-2024-09900', locale: 'nl-NL' }),
    applicationProperties: { retryCount: '5', smtpHost: 'smtp.internal' } },
  { messageId: 'msg-eml-002', subject: 'SendPasswordReset', contentType: 'application/json',
    body: JSON.stringify({ to: 'noreply@@broken.tld', templateId: 'pwd-reset', token: 'abc123', expiresInMinutes: 30 }),
    applicationProperties: { retryCount: '5', smtpHost: 'smtp.internal' } },
];

const emailsActive = [
  { messageId: 'msg-eml-003', subject: 'SendOrderConfirmation', contentType: 'application/json',
    body: JSON.stringify({ to: 'alice@demo.io', templateId: 'order-confirm', orderId: 'ORD-2024-10044', locale: 'en-US' }),
    applicationProperties: { retryCount: '0', smtpHost: 'smtp.sendgrid.net' } },
  { messageId: 'msg-eml-004', subject: 'SendShippingUpdate', contentType: 'application/json',
    body: JSON.stringify({ to: 'bob@demo.io', templateId: 'shipping-update', orderId: 'ORD-2024-10031', trackingCode: 'NL3829102', carrier: 'PostNL' }),
    applicationProperties: { retryCount: '0', smtpHost: 'smtp.sendgrid.net' } },
  { messageId: 'msg-eml-005', subject: 'SendPromotion', contentType: 'application/json',
    body: JSON.stringify({ to: 'carol@demo.io', templateId: 'promo-flash', discountCode: 'SAVE20', expiresAt: '2024-12-31' }),
    applicationProperties: { retryCount: '0', smtpHost: 'smtp.sendgrid.net' } },
  { messageId: 'msg-eml-006', subject: 'SendOrderConfirmation', contentType: 'application/json',
    body: JSON.stringify({ to: 'dave@demo.io', templateId: 'order-confirm', orderId: 'ORD-2024-10046', locale: 'de-DE' }),
    applicationProperties: { retryCount: '0', smtpHost: 'smtp.sendgrid.net' } },
];

const inventoryActive = [
  { messageId: 'msg-inv-001', subject: 'StockAdjusted', contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-780', warehouse: 'AMS-01', delta: -1, newStock: 34, reason: 'sale' }) },
  { messageId: 'msg-inv-002', subject: 'StockAdjusted', contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-999', warehouse: 'AMS-01', delta: -1, newStock: 2, reason: 'sale' }) },
  { messageId: 'msg-inv-003', subject: 'LowStockAlert', contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-999', warehouse: 'AMS-01', currentStock: 2, threshold: 5 }) },
  { messageId: 'msg-inv-004', subject: 'StockReplenished', contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-441', warehouse: 'RTD-02', delta: 200, newStock: 250, supplier: 'SUP-33' }) },
  { messageId: 'msg-inv-005', subject: 'StockAdjusted', contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-330', warehouse: 'RTD-02', delta: -1, newStock: 89, reason: 'sale' }) },
];

// Topic events
const orderEventMessages = [
  { messageId: 'msg-oe-001', subject: 'OrderPlaced',   contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10044', customerId: 'C-3391', totalAmount: 349.95, itemCount: 2, channel: 'web' }),
    applicationProperties: { eventType: 'OrderPlaced', schemaVersion: '1.2' } },
  { messageId: 'msg-oe-002', subject: 'OrderPlaced',   contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10045', customerId: 'C-7742', totalAmount: 59.00,  itemCount: 1, channel: 'mobile' }),
    applicationProperties: { eventType: 'OrderPlaced', schemaVersion: '1.2' } },
  { messageId: 'msg-oe-003', subject: 'OrderPlaced',   contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10046', customerId: 'C-1105', totalAmount: 1299.00, itemCount: 1, channel: 'web' }),
    applicationProperties: { eventType: 'OrderPlaced', schemaVersion: '1.2' } },
  { messageId: 'msg-oe-004', subject: 'OrderShipped',  contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10031', trackingCode: 'NL3829102', carrier: 'PostNL', estimatedDelivery: '2024-12-20' }),
    applicationProperties: { eventType: 'OrderShipped', schemaVersion: '1.2' } },
  { messageId: 'msg-oe-005', subject: 'OrderCancelled', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10042', reason: 'customer_request', refundAmount: 0 }),
    applicationProperties: { eventType: 'OrderCancelled', schemaVersion: '1.2' } },
  { messageId: 'msg-oe-006', subject: 'OrderDelivered', contentType: 'application/json',
    body: JSON.stringify({ orderId: 'ORD-2024-10020', deliveredAt: '2024-12-18T14:32:00Z', signedBy: 'A. Chen' }),
    applicationProperties: { eventType: 'OrderDelivered', schemaVersion: '1.2' } },
];

const userEventMessages = [
  { messageId: 'msg-ue-001', subject: 'UserRegistered',   contentType: 'application/json',
    body: JSON.stringify({ userId: 'C-9901', email: 'emma@demo.io',  plan: 'free',    locale: 'en-US', registeredAt: '2024-12-18T09:11:00Z' }),
    applicationProperties: { eventType: 'UserRegistered', source: 'web' } },
  { messageId: 'msg-ue-002', subject: 'UserRegistered',   contentType: 'application/json',
    body: JSON.stringify({ userId: 'C-9902', email: 'finn@demo.io',  plan: 'pro',     locale: 'de-DE', registeredAt: '2024-12-18T10:44:00Z' }),
    applicationProperties: { eventType: 'UserRegistered', source: 'mobile' } },
  { messageId: 'msg-ue-003', subject: 'ProfileUpdated',   contentType: 'application/json',
    body: JSON.stringify({ userId: 'C-3391', fields: ['address', 'phone'], updatedAt: '2024-12-18T11:02:00Z' }),
    applicationProperties: { eventType: 'ProfileUpdated', source: 'web' } },
  { messageId: 'msg-ue-004', subject: 'PasswordChanged',  contentType: 'application/json',
    body: JSON.stringify({ userId: 'C-7742', changedAt: '2024-12-18T12:15:00Z', ipAddress: '82.95.1.42' }),
    applicationProperties: { eventType: 'PasswordChanged', source: 'web' } },
  { messageId: 'msg-ue-005', subject: 'AccountSuspended', contentType: 'application/json',
    body: JSON.stringify({ userId: 'C-0099', reason: 'payment_failure', suspendedAt: '2024-12-18T08:00:00Z', autoResumeAt: '2024-12-25T08:00:00Z' }),
    applicationProperties: { eventType: 'AccountSuspended', source: 'billing-service' } },
];

const catalogMessages = [
  { messageId: 'msg-cat-001', subject: 'ProductPublished', contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-1100', name: 'Wireless Headphones Pro', category: 'electronics', price: 199.95, images: 4 }),
    applicationProperties: { eventType: 'ProductPublished' } },
  { messageId: 'msg-cat-002', subject: 'PriceUpdated',     contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-780', oldPrice: 399.00, newPrice: 349.95, reason: 'sale' }),
    applicationProperties: { eventType: 'PriceUpdated' } },
  { messageId: 'msg-cat-003', subject: 'ProductUpdated',   contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-330', fields: ['description', 'images'], updatedBy: 'content-team' }),
    applicationProperties: { eventType: 'ProductUpdated' } },
  { messageId: 'msg-cat-004', subject: 'ProductUnpublished', contentType: 'application/json',
    body: JSON.stringify({ sku: 'SKU-002', reason: 'out_of_season', unpublishedAt: '2024-12-18T07:00:00Z' }),
    applicationProperties: { eventType: 'ProductUnpublished' } },
];

// ── Main ───────────────────────────────────────────────────────────────────────

async function run() {
  console.log('Connecting to Service Bus emulator...');
  const client = new ServiceBusClient(CONNECTION_STRING);

  // Verify connectivity
  await withRetry('connectivity', async () => {
    const sender = client.createSender('order-processing');
    await sender.close();
    console.log('Connected.');
  });

  console.log('Seeding queues...');

  // order-processing: seed DLQ batch first, then active
  await withRetry('order-processing DLQ', async () => {
    await sendToQueue(client, 'order-processing', ordersDlq);
    await sleep(500);
    await dlqFromQueue(client, 'order-processing', ordersDlq.length,
      'ValidationFailed', 'Order failed schema validation: missing or invalid fields');
    await sendToQueue(client, 'order-processing', ordersActive);
  });
  console.log('  order-processing done');

  // payment-gateway: seed DLQ then active
  await withRetry('payment-gateway DLQ', async () => {
    await sendToQueue(client, 'payment-gateway', paymentsDlq);
    await sleep(500);
    await dlqFromQueue(client, 'payment-gateway', paymentsDlq.length,
      'PaymentProviderError', 'Provider returned non-retryable error after max retries exhausted');
    await sendToQueue(client, 'payment-gateway', paymentsActive);
  });
  console.log('  payment-gateway done');

  // email-notifications: seed DLQ then active
  await withRetry('email-notifications DLQ', async () => {
    await sendToQueue(client, 'email-notifications', emailsDlq);
    await sleep(500);
    await dlqFromQueue(client, 'email-notifications', emailsDlq.length,
      'DeliveryFailed', 'SMTP connection refused after 5 retries');
    await sendToQueue(client, 'email-notifications', emailsActive);
  });
  console.log('  email-notifications done');

  // inventory-sync: active only
  await withRetry('inventory-sync', () => sendToQueue(client, 'inventory-sync', inventoryActive));
  console.log('  inventory-sync done');

  console.log('Seeding topics...');

  // order-events topic → 3 subscriptions (fulfillment gets some DLQ)
  await withRetry('order-events', async () => {
    const sender = client.createSender('order-events');
    await sender.sendMessages(orderEventMessages);
    await sender.close();
    await sleep(500);
    await dlqFromSubscription(client, 'order-events', 'fulfillment', 2,
      'FulfillmentServiceUnavailable', 'Downstream fulfillment API returned 503');
  });
  console.log('  order-events done');

  // user-events topic → crm-sync gets some DLQ
  await withRetry('user-events', async () => {
    const sender = client.createSender('user-events');
    await sender.sendMessages(userEventMessages);
    await sender.close();
    await sleep(500);
    await dlqFromSubscription(client, 'user-events', 'crm-sync', 2,
      'CrmApiError', 'CRM API rate limit exceeded, message cannot be retried');
  });
  console.log('  user-events done');

  // catalog-updates topic → all active
  await withRetry('catalog-updates', async () => {
    const sender = client.createSender('catalog-updates');
    await sender.sendMessages(catalogMessages);
    await sender.close();
  });
  console.log('  catalog-updates done');

  await client.close();
  console.log('Seeding complete.');
}

run().catch(err => { console.error('Seeding failed:', err.message); process.exit(1); });
