# Multi-stack examples

## .NET (EF Core)

```csharp
// BAD — no ownership predicate
var order = await _db.Orders.FindAsync(id);
return Ok(order);

// GOOD
var order = await _db.Orders
    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == currentUser.Id);
if (order is null) return NotFound();
```

## Express + Sequelize

```js
// BAD
const order = await Order.findByPk(req.params.id);
res.json(order);

// GOOD
const order = await Order.findOne({
  where: { id: req.params.id, userId: req.user.id }
});
```

## FastAPI + SQLAlchemy

```python
# BAD
order = session.query(Order).get(order_id)

# GOOD
order = session.query(Order).filter(
    Order.id == order_id, Order.user_id == current_user.id
).first()
```

## Spring Data JPA

```java
// BAD
Order order = orderRepo.findById(id).orElseThrow();

// GOOD
Order order = orderRepo.findByIdAndUserId(id, currentUser.getId())
    .orElseThrow(() -> new ForbiddenException());
```
