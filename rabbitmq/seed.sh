#!/bin/sh
# Topology and sample messages for the RabbitMQ transport. Safe to rerun: declarations are idempotent,
# messages are only published into queues that report none.
set -eu

api() { # method path [json]
  curl -fsS -u "$CREDENTIALS" -X "$1" -H 'content-type: application/json' "$API$2" ${3:+--data "$3"}
}

until api GET /overview >/dev/null 2>&1; do sleep 1; done

publish() { # vhost exchange routing-key body [properties-json]
  api POST "/exchanges/$1/$2/publish" \
    "{\"routing_key\":\"$3\",\"payload\":$(printf '%s' "$4" | sed 's/\\/\\\\/g; s/"/\\"/g; s/^/"/; s/$/"/'),\"payload_encoding\":\"string\",\"properties\":${5:-{\}}}" >/dev/null
}

empty() { # vhost queue; a new queue reports no count until its first stats sample
  ! api GET "/queues/$1/$2?columns=messages" | grep -q '"messages":[1-9]'
}

V=%2F
api PUT "/vhosts/shop" '{"description":"Sample shop vhost: quorum by default","default_queue_type":"quorum"}'
api PUT "/permissions/shop/azsb" '{"configure":".*","write":".*","read":".*"}'

for vhost in "$V" shop; do
  api PUT "/exchanges/$vhost/orders" '{"type":"topic","durable":true}'
  api PUT "/exchanges/$vhost/dlx" '{"type":"direct","durable":true}'
  api PUT "/queues/$vhost/orders.dlq" '{"durable":true,"arguments":{"x-queue-type":"quorum"}}'
  api PUT "/queues/$vhost/payments.dlq" '{"durable":true,"arguments":{"x-queue-type":"classic"}}'
  api PUT "/queues/$vhost/orders" '{"durable":true,"arguments":{"x-queue-type":"quorum","x-delivery-limit":3,"x-dead-letter-exchange":"dlx","x-dead-letter-routing-key":"orders.dlq"}}'
  api PUT "/queues/$vhost/payments" '{"durable":true,"arguments":{"x-queue-type":"classic","x-dead-letter-exchange":"dlx","x-dead-letter-routing-key":"payments.dlq"}}'
  api PUT "/queues/$vhost/audit" '{"durable":true,"arguments":{"x-queue-type":"stream","x-max-age":"7D"}}'
  api PUT "/queues/$vhost/notifications" '{"durable":true,"arguments":{"x-queue-type":"classic"}}'
  api POST "/bindings/$vhost/e/orders/q/orders" '{"routing_key":"order.*"}'
  api POST "/bindings/$vhost/e/orders/q/payments" '{"routing_key":"order.paid"}'
  api POST "/bindings/$vhost/e/orders/q/audit" '{"routing_key":"#"}'
  api POST "/bindings/$vhost/e/dlx/q/orders.dlq" '{"routing_key":"orders.dlq"}'
  api POST "/bindings/$vhost/e/dlx/q/payments.dlq" '{"routing_key":"payments.dlq"}'
done
api PUT "/policies/$V/notifications-ttl" '{"pattern":"^notifications$","apply-to":"queues","definition":{"message-ttl":86400000}}'

json='{"content_type":"application/json","message_id":"%s","type":"%s","timestamp":%s,"headers":{"traceparent":"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01","source":"seed"}}'
for vhost in "$V" shop; do
  if empty "$vhost" orders; then
    for i in 1 2 3 4 5 6; do
      kind=$([ $((i % 2)) -eq 0 ] && echo paid || echo created)
      publish "$vhost" orders "order.$kind" "{\"orderId\":$i,\"status\":\"$kind\",\"total\":$((i * 12)).50}" \
        "$(printf "$json" "order-$i" "order-$kind" "$(date +%s)")"
    done
    # Reject two orders without requeue: they dead-letter into orders.dlq with an x-death header.
    api POST "/queues/$vhost/orders/get" '{"count":2,"ackmode":"reject_requeue_false","encoding":"auto"}' >/dev/null
  fi
  if empty "$vhost" notifications; then
    for i in 1 2 3; do
      publish "$vhost" amq.default notifications "Order $i shipped" '{"content_type":"text/plain"}'
    done
  fi
done

echo "Seeded: management UI at http://localhost:15680 (azsb/azsb), AMQP at localhost:5680"
