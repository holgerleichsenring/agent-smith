select
    order_id,
    customer_id,
    {{ dollars_to_cents('amount_cents') }} as amount
from {{ ref('stg_orders') }}
