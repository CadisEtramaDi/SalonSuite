$connString = "Server=.\SQLEXPRESS;Database=SalonSuiteDb;Integrated Security=True;"
$con = New-Object System.Data.SqlClient.SqlConnection($connString)
$con.Open()

$countCmd = $con.CreateCommand()
$countCmd.CommandText = "SELECT COUNT(*) FROM dbo.Services;"
$count = [int]$countCmd.ExecuteScalar()

if ($count -eq 0) {
    $insertCmd = $con.CreateCommand()
    $insertCmd.CommandText = @"
INSERT INTO dbo.Services (Number, Name, Category, Description, Price, DurationMinutes, IsActive) VALUES
('01', 'Bespoke Precision Haircut', 'Hair', 'Customized consultation and master haircut sculpted to complement your facial morphology. Includes botanical shampoo and blowout.', 850.00, 45, 1),
('02', 'Red Carpet Blowout & Styling', 'Styling', 'Voluminous runway blowout with thermal shine finish, bouncy curls, or silky glass hair sculpting.', 650.00, 35, 1),
('03', 'Bridal & Gala Updo Hairstyle', 'Styling', 'Intricate formal wedding updo, romantic braided chignon, or red-carpet event styling with long-lasting hold.', 1800.00, 60, 1),
('04', 'Beach Waves & Hollywood Curls Hairstyle', 'Styling', 'Effortless mermaid texture, undone beach waves, or cascading Hollywood glamour curls with thermal protection.', 750.00, 40, 1),
('05', 'French Dimensional Balayage & Glaze', 'Color', 'Hand-painted dimensional balayage with gloss glaze for seamless sun-kissed radiance, soft regrowth, and mirror shine.', 1500.00, 90, 1),
('06', 'Full Foil Highlights & Tone', 'Color', 'Multidimensional precision foil highlights with seamless root shadow and customized gloss toning.', 1400.00, 75, 1),
('07', 'Luminous Gloss & Neutralizing Toner', 'Color', 'Translucent demi-permanent color glaze that cancels unwanted brassiness, seals cuticle porosity, and infuses multi-angle reflection.', 750.00, 30, 1),
('08', 'Root Regrowth & Gray Coverage', 'Color', '100% seamless root regrowth coverage using rich, conditioning European organic pigments.', 1100.00, 60, 1),
('09', 'Keratin Silk Protein Smoothing Ritual', 'Treatment', 'Intensive protein bond restructuring therapy that eliminates frizz, blocks humidity, and restores silky elasticity for 12 weeks.', 950.00, 60, 1),
('10', 'Deep Collagen & Moisture Mask Therapy', 'Treatment', 'Hydrolyzed marine collagen and argan lipid mask with micro-mist steam infusion for bleached, brittle, or dry hair.', 700.00, 40, 1),
('11', 'Botanical Scalp Detox & Head Spa', 'Spa', 'Clarifying sea salt scrub, organic tonic infusion, ozone mist hydration, and rejuvenating Japanese shiatsu pressure massage.', 1200.00, 50, 1),
('12', 'Gentlemen''s Precision Fade & Beard Sculpting', 'Hair', 'Clean scissor & clipper fade haircut, invigorating hot towel treatment, neck razor line, and conditioning beard balm.', 600.00, 35, 1);
"@
    $insertCmd.ExecuteNonQuery()
    Write-Host "Inserted 12 individual services and hairstyles into dbo.Services!"
} else {
    Write-Host "dbo.Services already has $count services."
}

$con.Close()
