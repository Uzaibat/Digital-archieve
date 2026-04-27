INSERT INTO users ("Username", "Email", "PasswordHash", "Role", "CreatedDate")
VALUES ('admin', 'admin@idadrs.local', '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'Admin', NOW())
ON CONFLICT ("Username") DO UPDATE SET "Role" = 'Admin';

INSERT INTO categories ("CategoryName", "Description")
VALUES
  ('Reports', 'Financial and operational reports'),
  ('Contracts', 'Legal contracts and agreements'),
  ('Presentations', 'Slide decks and presentations'),
  ('Manuals', 'User manuals and documentation'),
  ('Images', 'Photos and image files')
ON CONFLICT DO NOTHING;
