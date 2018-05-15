require 'ElencyConfig/HMACSHA256'

class Client

  def initialize()
    @initialized = 10
  end

  def hi()
    hasher = HMACSHA256.new()
    hashedValue = hasher.hash("TheValueToEncrypt", "aGVsbG93b3JsZA==")
    puts hashedValue
    puts @initialized
  end

  def set(value)
    @initialized = value
  end

  def get
    puts @initialized
  end

end