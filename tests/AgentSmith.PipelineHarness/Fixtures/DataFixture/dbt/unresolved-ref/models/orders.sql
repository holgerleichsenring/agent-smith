select
    order_id,
    customer_id,
    {{ cents_to_dollars('amount_cents') }} as amount
from {{ ref('stg_ordrs') }}
